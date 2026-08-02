using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPositionMarketRuntimeContract
{
    public const string DraftReady =
        "ARCH7B_POSITION_MARKET_SLOT_BINDING_DRAFT_READY";
    public const string DraftPublicationFailed =
        "ARCH7B_POSITION_MARKET_DRAFT_PUBLICATION_FAILED";
    public const string DraftEvidenceShaMismatch =
        "ARCH7B_POSITION_MARKET_DRAFT_EVIDENCE_SHA_MISMATCH";
    public const string DraftAlreadyExists =
        "ARCH7B_POSITION_MARKET_DRAFT_ALREADY_EXISTS";
    public const string DraftNotBoundToSelectedSnapshot =
        "ARCH7B_POSITION_MARKET_DRAFT_NOT_BOUND_TO_SELECTED_SNAPSHOT";
    public const string DraftRequiredBeforeMarketCapture =
        "ARCH7B_POSITION_MARKET_DRAFT_REQUIRED_BEFORE_MARKET_CAPTURE";
    public const string LineageFinalizationFailed =
        "ARCH7B_POSITION_MARKET_LINEAGE_FINALIZATION_FAILED";
    public const string LineageNotInMarketManifest =
        "ARCH7B_POSITION_MARKET_LINEAGE_NOT_IN_MARKET_MANIFEST";
    public const string LineageNotInReadyMarker =
        "ARCH7B_POSITION_MARKET_LINEAGE_NOT_IN_READY_MARKER";
    public const string CoverageIncomplete =
        "ARCH7B_POSITION_MARKET_COVERAGE_INCOMPLETE";
    public const string ProjectionCardinalityMismatch =
        "ARCH7B_POSITION_MARKET_PROJECTION_CARDINALITY_MISMATCH";
    public const string RevisionBindingRequired =
        "ARCH7B_POSITION_MARKET_REVISION_BINDING_REQUIRED";
    public const string RevisionBindingShaMismatch =
        "ARCH7B_POSITION_MARKET_REVISION_BINDING_SHA_MISMATCH";
    public const string Arch7aBindingRequired =
        "ARCH7B_ARCH7A_POSITION_MARKET_REVISION_BINDING_REQUIRED";
    public const string ReplayLineageMismatch =
        "ARCH7B_POSITION_MARKET_REPLAY_LINEAGE_MISMATCH";
    public const string ReplayMarketManifestMismatch =
        "ARCH7B_POSITION_MARKET_REPLAY_MARKET_MANIFEST_MISMATCH";
}

public sealed record Arch7bContentAddressedFile(string Path, string Sha256);

public sealed record Arch7bPositionMarketFinalization(
    Arch7bPositionMarketSlotLineage Lineage,
    Arch7bContentAddressedFile LineageFile,
    string MarketManifestPreLineageSha256,
    string PublishedMarketManifestSha256);

public sealed record Arch7bPositionMarketImportAuthority(
    string DraftPath,
    string DraftSha256,
    string LineagePath,
    string LineageSha256,
    string RevisionBindingPath,
    PmsShadowFreshSlotReadyMarker ReadyMarker);

public static class Arch7bPositionMarketLineageFileStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static Arch7bContentAddressedFile WriteDraftCreateNew(
        string path, Arch7bPositionMarketSlotBindingDraft draft)
    {
        Arch7bPositionMarketSlotLineageContract.ValidateDraft(draft);
        return WriteCreateNew(path, draft,
            Arch7bPositionMarketRuntimeContract.DraftAlreadyExists, false);
    }

    public static Arch7bPositionMarketSlotBindingDraft ReadDraft(
        string path, string expectedFileSha256)
    {
        var value = Read<Arch7bPositionMarketSlotBindingDraft>(path,
            expectedFileSha256,
            Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture,
            Arch7bPositionMarketRuntimeContract.DraftEvidenceShaMismatch);
        Arch7bPositionMarketSlotLineageContract.ValidateDraft(value);
        return value;
    }

    public static Arch7bContentAddressedFile WriteLineageCreateNew(
        string path, Arch7bPositionMarketSlotLineage lineage)
    {
        Arch7bPositionMarketSlotLineageContract.Validate(lineage);
        return WriteCreateNew(path, lineage,
            Arch7bPositionMarketRuntimeContract.LineageFinalizationFailed, false);
    }

    public static Arch7bPositionMarketSlotLineage ReadLineage(
        string path, string expectedFileSha256)
    {
        var value = Read<Arch7bPositionMarketSlotLineage>(path,
            expectedFileSha256,
            Arch7bPositionMarketRuntimeContract.LineageNotInMarketManifest,
            Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);
        Arch7bPositionMarketSlotLineageContract.Validate(value);
        return value;
    }

    public static Arch7bContentAddressedFile WriteRevisionBindingIdempotent(
        string path, Arch7bEconomicRevisionInputBinding binding)
    {
        Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
            binding, binding.ProjectionRevisionId);
        return WriteCreateNew(path, binding,
            Arch7bPositionMarketRuntimeContract.ReplayLineageMismatch, true);
    }

    public static Arch7bEconomicRevisionInputBinding ReadRevisionBinding(
        string path, string expectedFileSha256)
    {
        var value = Read<Arch7bEconomicRevisionInputBinding>(path,
            expectedFileSha256,
            Arch7bPositionMarketRuntimeContract.RevisionBindingRequired,
            Arch7bPositionMarketRuntimeContract.RevisionBindingShaMismatch);
        Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
            value, value.ProjectionRevisionId);
        return value;
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static IReadOnlyList<string> ReadRequiredMarketSymbols(
        string configPath, string expectedConfigSha256)
    {
        RequireAbsolute(configPath);
        RequireSha(expectedConfigSha256,
            Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);
        if (!File.Exists(configPath) || Sha256File(configPath) != expectedConfigSha256)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);
        using var document = JsonDocument.Parse(File.ReadAllBytes(configPath));
        if (!document.RootElement.TryGetProperty("instruments", out var instruments) ||
            instruments.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);
        var values = instruments.EnumerateArray().Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>()
            .ToArray();
        if (values.Length != Arch7bPositionMarketSlotLineageContract.RequiredMarketCoverageCount ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);
        return values;
    }

    private static T Read<T>(string path, string expectedFileSha256,
        string missingBlocker, string shaBlocker)
    {
        RequireAbsolute(path);
        RequireSha(expectedFileSha256, shaBlocker);
        if (!File.Exists(path)) throw new InvalidDataException(missingBlocker);
        if (Sha256File(path) != expectedFileSha256)
            throw new InvalidDataException(shaBlocker);
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException(missingBlocker);
    }

    private static Arch7bContentAddressedFile WriteCreateNew<T>(string path,
        T value, string conflictBlocker, bool allowIdentical)
    {
        RequireAbsolute(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var bytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(value, Json) + Environment.NewLine);
        if (File.Exists(fullPath))
        {
            if (allowIdentical && File.ReadAllBytes(fullPath).SequenceEqual(bytes))
                return new(fullPath, Sha256File(fullPath));
            throw new InvalidDataException(conflictBlocker);
        }
        var temporary = fullPath + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, fullPath);
        }
        catch (IOException) when (File.Exists(fullPath))
        {
            if (allowIdentical && File.ReadAllBytes(fullPath).SequenceEqual(bytes))
                return new(fullPath, Sha256File(fullPath));
            throw new InvalidDataException(conflictBlocker);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return new(fullPath, Sha256File(fullPath));
    }

    internal static void RequireAbsolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.BindingRequired);
    }

    internal static void RequireSha(string value, string blocker)
    {
        if (!Arch5bHashing.IsSha256(value) || value.Any(char.IsUpper))
            throw new InvalidDataException(blocker);
    }
}

public static class Arch7bPositionMarketLiveWiring
{
    public static (Arch7bPositionMarketSlotBindingDraft Draft,
        Arch7bContentAddressedFile File) BuildAndPublishDraft(
        string draftPath,
        string runId,
        string account,
        string targetProfile,
        string coreCommit,
        string intradayCommit,
        PmsShadowEconomicSource source,
        PmsShadowIntradaySlotWindow slot,
        string marketCaptureSessionId,
        IReadOnlyCollection<string> requiredMarketSymbols)
    {
        RequireFileName(draftPath, "position-market-slot-binding-draft.json");
        var draft = Arch7bPositionMarketSlotLineageContract.BuildDraft(
            runId, account, targetProfile, coreCommit, intradayCommit, source,
            slot, marketCaptureSessionId, requiredMarketSymbols);
        var file = Arch7bPositionMarketLineageFileStore.WriteDraftCreateNew(
            draftPath, draft);
        return (draft, file);
    }

    public static Arch7bPositionMarketSlotBindingDraft RequirePrearmedDraft(
        string draftPath,
        string expectedDraftSha256,
        PmsShadowIntradaySlotWindow slot,
        string marketCaptureSessionId,
        string coreCommit,
        string intradayCommit,
        IReadOnlyCollection<string> requiredMarketSymbols)
    {
        var draft = Arch7bPositionMarketLineageFileStore.ReadDraft(
            draftPath, expectedDraftSha256);
        if (draft.SlotId != slot.SlotId || draft.SlotStartUtc != slot.SlotStartUtc ||
            draft.SlotEndUtc != slot.SlotEndUtc ||
            draft.MarketCaptureSessionId != marketCaptureSessionId ||
            draft.CoreCommit != coreCommit || draft.IntradayCommit != intradayCommit ||
            draft.RequiredMarketSymbolSetSha256 !=
            Arch7bPositionMarketSlotLineageContract.RequiredMarketSymbolSetSha256(
                requiredMarketSymbols))
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot);
        return draft;
    }

    public static Arch7bPositionMarketFinalization FinalizeMarket(
        string manifestPath,
        string draftPath,
        string expectedDraftSha256,
        string lineagePath,
        PmsShadowSlotBboSelection selection,
        PmsShadowCaptureClockAuthorityEvidence clockAuthority)
    {
        RequireFileName(lineagePath, "position-market-slot-lineage.json");
        var draft = Arch7bPositionMarketLineageFileStore.ReadDraft(
            draftPath, expectedDraftSha256);
        if (!selection.Qualifying || selection.SelectedBySymbol.Count !=
            Arch7bPositionMarketSlotLineageContract.RequiredMarketCoverageCount ||
            selection.SelectionSha256 is null ||
            selection.MinimumSelectedSourceTimestampUtc is null ||
            selection.MaximumSelectedSourceTimestampUtc is null)
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.CoverageIncomplete);

        manifestPath = Path.GetFullPath(manifestPath);
        var root = JsonNode.Parse(File.ReadAllBytes(manifestPath))?.AsObject()
            ?? throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.LineageFinalizationFailed);
        if (RequiredString(root, "slot_id") != draft.SlotId ||
            RequiredString(root, "recorder_run_id") != draft.MarketCaptureSessionId ||
            RequiredString(root, "selection_sha256") != selection.SelectionSha256 ||
            RequiredString(root, "clock_authority_snapshot_sha256") !=
            clockAuthority.PreCapture.SnapshotSha256 ||
            RequiredString(root, "clock_post_close_snapshot_sha256") !=
            clockAuthority.PostClose.SnapshotSha256)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);
        var symbolSet = root["last_bbo_by_symbol"]?.AsObject().Select(value => value.Key)
            .ToArray() ?? [];
        if (Arch7bPositionMarketSlotLineageContract.RequiredMarketSymbolSetSha256(
                symbolSet) != draft.RequiredMarketSymbolSetSha256)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.MappingAuthorityMismatch);

        var preLineageManifestSha =
            Arch7bPositionMarketLineageFileStore.Sha256File(manifestPath);
        var lineage = Arch7bPositionMarketSlotLineageContract.Finalize(
            draft, clockAuthority.PreCapture.SnapshotSha256,
            clockAuthority.PostClose.SnapshotSha256,
            selection.SelectionSha256, preLineageManifestSha,
            selection.SelectedBySymbol.Count,
            selection.MinimumSelectedSourceTimestampUtc.Value,
            selection.MaximumSelectedSourceTimestampUtc.Value);
        var lineageFile =
            Arch7bPositionMarketLineageFileStore.WriteLineageCreateNew(
                lineagePath, lineage);

        root["position_market_lineage_contract"] = lineage.ContractVersion;
        root["position_market_lineage_path"] = lineageFile.Path;
        root["position_market_lineage_sha256"] = lineageFile.Sha256;
        root["position_market_lineage_evidence_sha256"] = lineage.EvidenceSha256;
        root["selected_position_snapshot_id"] =
            lineage.SelectedPositionSnapshotId.ToString("D");
        root["position_snapshot_line_set_sha256"] =
            lineage.PositionSnapshotLineSetSha256;
        root["source_ingestion_id"] = lineage.SourceIngestionId.ToString("D");
        root["source_session_id"] = lineage.SourceSessionId;
        root["required_pms_universe_sha256"] = lineage.RequiredPmsUniverseSha256;
        root["required_market_symbol_set_sha256"] =
            lineage.RequiredMarketSymbolSetSha256;
        root["market_mapping_contract_version"] =
            lineage.MarketMappingContractVersion;
        root["market_mapping_set_sha256"] = lineage.MarketMappingSetSha256;
        root["market_capture_session_id"] = lineage.MarketCaptureSessionId;
        root["market_manifest_pre_lineage_sha256"] = preLineageManifestSha;
        WriteAtomicReplace(manifestPath, root);
        return new(lineage, lineageFile, preLineageManifestSha,
            Arch7bPositionMarketLineageFileStore.Sha256File(manifestPath));
    }

    public static Arch7bPositionMarketSlotLineage RequireImportBinding(
        string manifestPath,
        Arch7bPositionMarketImportAuthority authority,
        PmsShadowEconomicSource source,
        PmsShadowRealSlotCapture capture)
    {
        var draft = Arch7bPositionMarketLineageFileStore.ReadDraft(
            authority.DraftPath, authority.DraftSha256);
        var lineage = Arch7bPositionMarketLineageFileStore.ReadLineage(
            authority.LineagePath, authority.LineageSha256);
        var expected = Arch7bPositionMarketSlotLineageContract.Finalize(
            draft, lineage.ClockCaptureStartEvidenceSha256,
            lineage.ClockPostCloseEvidenceSha256, lineage.MarketSelectionSha256,
            lineage.MarketManifestSha256, lineage.MarketCoverageCount,
            lineage.SourceTimestampStartUtc, lineage.SourceTimestampEndUtc);
        Arch7bPositionMarketSlotLineageContract.RequireExactBinding(expected, lineage);

        if (authority.ReadyMarker.PositionMarketLineagePath != authority.LineagePath ||
            authority.ReadyMarker.PositionMarketLineageSha256 != authority.LineageSha256)
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.LineageNotInReadyMarker);
        manifestPath = Path.GetFullPath(manifestPath);
        if (Arch7bPositionMarketLineageFileStore.Sha256File(manifestPath) !=
            authority.ReadyMarker.ManifestSha256)
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var root = document.RootElement;
        if (!HasExactString(root, "position_market_lineage_contract",
                lineage.ContractVersion) ||
            !HasExactString(root, "position_market_lineage_path",
                authority.LineagePath) ||
            !HasExactString(root, "position_market_lineage_sha256",
                authority.LineageSha256) ||
            !HasExactString(root, "position_market_lineage_evidence_sha256",
                lineage.EvidenceSha256) ||
            !HasExactString(root, "market_manifest_pre_lineage_sha256",
                lineage.MarketManifestSha256))
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.LineageNotInMarketManifest);

        var rebound = Arch7bPositionMarketSlotLineageContract.BuildDraft(
            draft.RunId, draft.Account, draft.TargetProfile, draft.CoreCommit,
            draft.IntradayCommit, source,
            new PmsShadowIntradaySlotWindow(draft.SlotId, draft.SlotStartUtc,
                draft.SlotEndUtc, DateOnly.FromDateTime(draft.SlotEndUtc.UtcDateTime)),
            draft.MarketCaptureSessionId,
            capture.Bbo.Select(value => value.Symbol).ToArray());
        if (rebound != draft || capture.RecorderRunId != draft.MarketCaptureSessionId ||
            capture.SlotId != draft.SlotId || !capture.NoOrder)
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.DraftNotBoundToSelectedSnapshot);
        return lineage;
    }

    public static (Arch7bEconomicRevisionInputBinding Binding,
        Arch7bContentAddressedFile File) BindAndPublishRevision(
        Arch7bPositionMarketSlotLineage lineage,
        PmsShadowIntradayEconomicProjection projection,
        string revisionBindingPath)
    {
        RequireFileName(revisionBindingPath,
            "position-market-revision-input-binding.json");
        if (projection.MarketData.Count != 99 ||
            projection.TargetPositions.Count != 288 ||
            projection.PositionOnlyDrifts.Count != 288)
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.ProjectionCardinalityMismatch);
        var binding = Arch7bPositionMarketSlotLineageContract.BindRevision(
            lineage, projection);
        var file =
            Arch7bPositionMarketLineageFileStore.WriteRevisionBindingIdempotent(
                revisionBindingPath, binding);
        return (binding, file);
    }

    public static Arch7bEconomicRevisionInputBinding RequireArch7aRevision(
        string bindingPath, string expectedBindingSha256,
        Guid arch7aEconomicRevisionId)
    {
        var binding = Arch7bPositionMarketLineageFileStore.ReadRevisionBinding(
            bindingPath, expectedBindingSha256);
        try
        {
            Arch7bPositionMarketSlotLineageContract.RequireArch7aRevision(
                binding, arch7aEconomicRevisionId);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.Arch7aBindingRequired);
        }
        return binding;
    }

    private static void RequireFileName(string path, string expected)
    {
        Arch7bPositionMarketLineageFileStore.RequireAbsolute(path);
        if (!string.Equals(Path.GetFileName(path), expected,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.BindingRequired);
    }

    private static string RequiredString(JsonObject value, string name) =>
        value[name]?.GetValue<string>() ??
        throw new InvalidDataException(
            Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);

    private static string RequiredString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        property.GetString() is { } result
            ? result
            : throw new InvalidDataException(
                Arch7bPositionMarketSlotLineageContract.ManifestBindingMismatch);

    private static bool HasExactString(
        JsonElement value, string name, string expected) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        string.Equals(property.GetString(), expected, StringComparison.Ordinal);

    private static void WriteAtomicReplace(string path, JsonObject value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        var temporary = path + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
