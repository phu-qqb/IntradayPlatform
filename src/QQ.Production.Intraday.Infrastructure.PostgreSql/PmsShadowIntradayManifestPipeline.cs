using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed class FinalizedPmsShadowIntradayManifestPipeline(string finalizedHandoffRoot)
    : IPmsShadowIntradaySlotPipeline
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<PmsShadowIntradaySlotManifest> ExecuteAsync(PmsShadowIntradaySlotWindow slot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(finalizedHandoffRoot))
            throw new InvalidOperationException("FINALIZED_HANDOFF_ROOT_REQUIRED");
        var root = Path.GetFullPath(finalizedHandoffRoot);
        var path = Path.GetFullPath(Path.Combine(root, $"{slot.SlotId}.json"));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FINALIZED_HANDOFF_PATH_ESCAPE_REJECTED");
        if (!File.Exists(path)) throw new InvalidDataException("INTRADAY_SLOT_INCOMPLETE");

        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<PmsShadowIntradaySlotManifest>(stream,
            Json, cancellationToken) ?? throw new InvalidDataException("INTRADAY_SLOT_INCOMPLETE");
        if (manifest.SlotId != slot.SlotId || manifest.SlotStartUtc != slot.SlotStartUtc ||
            manifest.SlotEndUtc != slot.SlotEndUtc)
            throw new InvalidDataException("SLOT_WINDOW_IDENTITY_MISMATCH");
        var validation = PmsShadowIntradayManifestValidation.Validate(manifest);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(';', validation.Issues));
        return manifest;
    }
}
