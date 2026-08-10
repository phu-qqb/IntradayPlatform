using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bOperationalCatalogMaterialization(
    string ContractVersion,
    string CatalogPath,
    string CatalogSha256,
    string MarkerInventoryPath,
    string MarkerInventorySha256,
    string ProducerAuditPath,
    string ProducerAuditSha256,
    int CommandCount,
    int BindingCount,
    int MarkerCount,
    int MissingProducerCount,
    Arch7bNoLiveSafetyCounters Safety);

public static class Arch7bOperationalCatalogMaterializer
{
    public const string Version = "arch7b_operational_catalog_materialization_v1";
    public const string SourceManifestLabel =
        "docs/architecture/arch7b/arch7b-position-market-live-command-manifest.json";
    public const string CatalogFileName =
        "arch7b-operational-live-fact-binding-catalog-v1.json";
    public const string MarkerInventoryFileName =
        "arch7b-v7-six-command-marker-inventory.json";
    public const string ProducerAuditFileName =
        "arch7b-operational-binding-producer-audit-v1.json";

    public static async Task<Arch7bOperationalCatalogMaterialization> MaterializeAsync(
        string sourceManifestPath, string outputRoot,
        CancellationToken cancellationToken = default)
    {
        sourceManifestPath = Path.GetFullPath(sourceManifestPath);
        outputRoot = Path.GetFullPath(outputRoot);
        var sourceBytes = await File.ReadAllBytesAsync(sourceManifestPath, cancellationToken)
            .ConfigureAwait(false);
        var catalog = Arch7bOperationalLiveFactBindingCatalog.Document();
        var audit = Arch7bOperationalBindingProducerAudit.Build();
        var inventory = Arch7bOperationalLiveFactBindingCatalog.InventoryMarkers(
            SourceManifestLabel, sourceBytes);
        Directory.CreateDirectory(outputRoot);
        var catalogPath = Path.Combine(outputRoot, CatalogFileName);
        var inventoryPath = Path.Combine(outputRoot, MarkerInventoryFileName);
        var auditPath = Path.Combine(outputRoot, ProducerAuditFileName);
        var catalogBytes = JsonSerializer.SerializeToUtf8Bytes(catalog,
            Arch7bJson.CanonicalOptions);
        var inventoryBytes = JsonSerializer.SerializeToUtf8Bytes(inventory,
            Arch7bJson.CanonicalOptions);
        var auditBytes = JsonSerializer.SerializeToUtf8Bytes(audit,
            Arch7bJson.CanonicalOptions);
        await File.WriteAllBytesAsync(catalogPath, catalogBytes, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(inventoryPath, inventoryBytes, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(auditPath, auditBytes, cancellationToken)
            .ConfigureAwait(false);
        return new(Version, catalogPath, Sha(catalogBytes), inventoryPath,
            Sha(inventoryBytes), auditPath, Sha(auditBytes), catalog.CommandCount,
            catalog.BindingCount, inventory.MarkerCount, audit.MissingProducerCount,
            Arch7bNoLiveSafetyCounters.Zero);
    }

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
