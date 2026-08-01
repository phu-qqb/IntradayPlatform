using System.Globalization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPositionMarketSlotLineageContract
{
    public const string Version = "arch7b_position_market_slot_lineage_v1";
    public const string MarketMappingContractVersion =
        "arch6a_lmax_usd_cross_rate_projection_v1";
    public const string EconomicRevisionInputBindingVersion =
        "arch7b_position_market_revision_input_binding_v1";
    public const int RequiredPositionCount = 99;
    public const int RequiredMarketCoverageCount = 49;
    public const int MaximumPositionAgeMilliseconds = 300_000;

    public const string BindingRequired =
        "ARCH7B_POSITION_MARKET_SLOT_BINDING_REQUIRED";
    public const string PositionSnapshotMismatch =
        "ARCH7B_POSITION_MARKET_POSITION_SNAPSHOT_MISMATCH";
    public const string SourceIngestionMismatch =
        "ARCH7B_POSITION_MARKET_SOURCE_INGESTION_MISMATCH";
    public const string RequiredUniverseMismatch =
        "ARCH7B_POSITION_MARKET_REQUIRED_UNIVERSE_MISMATCH";
    public const string SlotMismatch =
        "ARCH7B_POSITION_MARKET_SLOT_MISMATCH";
    public const string MappingAuthorityMismatch =
        "ARCH7B_POSITION_MARKET_MAPPING_AUTHORITY_MISMATCH";
    public const string ManifestBindingMismatch =
        "ARCH7B_POSITION_MARKET_MANIFEST_BINDING_MISMATCH";
    public const string RevisionInputMismatch =
        "ARCH7B_POSITION_MARKET_REVISION_INPUT_MISMATCH";

    public static Arch7bPositionMarketSlotBindingDraft BuildDraft(
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
        if (source.CurrentPositions.Count != RequiredPositionCount ||
            source.Mappings.Count != RequiredPositionCount)
            throw new InvalidDataException(RequiredUniverseMismatch);
        if (requiredMarketSymbols.Count != RequiredMarketCoverageCount ||
            requiredMarketSymbols.Distinct(StringComparer.Ordinal).Count() !=
            RequiredMarketCoverageCount)
            throw new InvalidDataException(MappingAuthorityMismatch);

        var age = slot.SlotStartUtc - source.PositionAsOfUtc;
        var value = new Arch7bPositionMarketSlotBindingDraft(
            Version, runId, account, targetProfile, coreCommit, intradayCommit,
            source.PositionSnapshotId, source.PositionAsOfUtc,
            PositionLineSetSha256(source), source.PositionAuthority,
            source.IngestionId, source.SourceSessionId,
            RequiredPmsUniverseSha256(source.Mappings),
            slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc,
            marketCaptureSessionId,
            RequiredMarketSymbolSetSha256(requiredMarketSymbols),
            MarketMappingContractVersion, MappingSetSha256(source.Mappings),
            checked((long)age.TotalMilliseconds), true, string.Empty);
        value = value with { EvidenceSha256 = DraftEvidenceSha256(value) };
        ValidateDraft(value);
        return value;
    }

    public static Arch7bPositionMarketSlotLineage Finalize(
        Arch7bPositionMarketSlotBindingDraft draft,
        string clockCaptureStartEvidenceSha256,
        string clockPostCloseEvidenceSha256,
        string marketSelectionSha256,
        string marketManifestSha256,
        int marketCoverageCount,
        DateTimeOffset sourceTimestampStartUtc,
        DateTimeOffset sourceTimestampEndUtc)
    {
        ValidateDraft(draft);
        var value = new Arch7bPositionMarketSlotLineage(
            draft.ContractVersion, draft.RunId, draft.Account, draft.TargetProfile,
            draft.CoreCommit, draft.IntradayCommit,
            draft.SelectedPositionSnapshotId, draft.PositionSnapshotAsOfUtc,
            draft.PositionSnapshotLineSetSha256, draft.PositionAuthority,
            draft.SourceIngestionId, draft.SourceSessionId,
            draft.RequiredPmsUniverseSha256, draft.SlotId, draft.SlotStartUtc,
            draft.SlotEndUtc, draft.MarketCaptureSessionId,
            draft.RequiredMarketSymbolSetSha256,
            draft.MarketMappingContractVersion, draft.MarketMappingSetSha256,
            clockCaptureStartEvidenceSha256, clockPostCloseEvidenceSha256,
            marketSelectionSha256, marketManifestSha256,
            draft.PositionSnapshotAgeAtSlotStartMilliseconds,
            marketCoverageCount, sourceTimestampStartUtc, sourceTimestampEndUtc,
            EconomicRevisionInputBindingVersion, true, string.Empty);
        value = value with { EvidenceSha256 = LineageEvidenceSha256(value) };
        Validate(value);
        return value;
    }

    public static void ValidateDraft(Arch7bPositionMarketSlotBindingDraft? value)
    {
        if (value is null) throw new InvalidDataException(BindingRequired);
        RequireCommon(value.ContractVersion, value.RunId, value.Account,
            value.TargetProfile, value.CoreCommit, value.IntradayCommit,
            value.SlotId, value.SlotStartUtc, value.SlotEndUtc,
            value.MarketCaptureSessionId, value.NoOrder);
        RequireSha(value.PositionSnapshotLineSetSha256, PositionSnapshotMismatch);
        RequireSha(value.RequiredPmsUniverseSha256, RequiredUniverseMismatch);
        RequireSha(value.RequiredMarketSymbolSetSha256, MappingAuthorityMismatch);
        RequireSha(value.MarketMappingSetSha256, MappingAuthorityMismatch);
        if (value.MarketMappingContractVersion != MarketMappingContractVersion)
            throw new InvalidDataException(MappingAuthorityMismatch);
        RequirePositionTime(value.PositionSnapshotAsOfUtc, value.SlotStartUtc,
            value.PositionSnapshotAgeAtSlotStartMilliseconds);
        if (value.EvidenceSha256 != DraftEvidenceSha256(value))
            throw new InvalidDataException(ManifestBindingMismatch);
    }

    public static void Validate(Arch7bPositionMarketSlotLineage? value)
    {
        if (value is null) throw new InvalidDataException(BindingRequired);
        RequireCommon(value.ContractVersion, value.RunId, value.Account,
            value.TargetProfile, value.CoreCommit, value.IntradayCommit,
            value.SlotId, value.SlotStartUtc, value.SlotEndUtc,
            value.MarketCaptureSessionId, value.NoOrder);
        RequireSha(value.PositionSnapshotLineSetSha256, PositionSnapshotMismatch);
        RequireSha(value.RequiredPmsUniverseSha256, RequiredUniverseMismatch);
        RequireSha(value.RequiredMarketSymbolSetSha256, MappingAuthorityMismatch);
        RequireSha(value.MarketMappingSetSha256, MappingAuthorityMismatch);
        RequireSha(value.ClockCaptureStartEvidenceSha256, ManifestBindingMismatch);
        RequireSha(value.ClockPostCloseEvidenceSha256, ManifestBindingMismatch);
        RequireSha(value.MarketSelectionSha256, ManifestBindingMismatch);
        RequireSha(value.MarketManifestSha256, ManifestBindingMismatch);
        if (value.MarketMappingContractVersion != MarketMappingContractVersion)
            throw new InvalidDataException(MappingAuthorityMismatch);
        if (value.EconomicRevisionInputBindingVersion !=
            EconomicRevisionInputBindingVersion)
            throw new InvalidDataException(RevisionInputMismatch);
        RequirePositionTime(value.PositionSnapshotAsOfUtc, value.SlotStartUtc,
            value.PositionSnapshotAgeAtSlotStartMilliseconds);
        if (value.MarketCoverageCount != RequiredMarketCoverageCount ||
            value.SourceTimestampStartUtc < value.SlotStartUtc ||
            value.SourceTimestampEndUtc > value.SlotEndUtc ||
            value.SourceTimestampEndUtc < value.SourceTimestampStartUtc)
            throw new InvalidDataException(ManifestBindingMismatch);
        if (value.EvidenceSha256 != LineageEvidenceSha256(value))
            throw new InvalidDataException(ManifestBindingMismatch);
    }

    public static void RequireExactBinding(
        Arch7bPositionMarketSlotLineage expected,
        Arch7bPositionMarketSlotLineage actual)
    {
        Validate(expected);
        Validate(actual);
        if (actual.SelectedPositionSnapshotId != expected.SelectedPositionSnapshotId ||
            actual.PositionSnapshotLineSetSha256 != expected.PositionSnapshotLineSetSha256 ||
            actual.PositionSnapshotAsOfUtc != expected.PositionSnapshotAsOfUtc)
            throw new InvalidDataException(PositionSnapshotMismatch);
        if (actual.SourceIngestionId != expected.SourceIngestionId ||
            actual.SourceSessionId != expected.SourceSessionId)
            throw new InvalidDataException(SourceIngestionMismatch);
        if (actual.RequiredPmsUniverseSha256 != expected.RequiredPmsUniverseSha256)
            throw new InvalidDataException(RequiredUniverseMismatch);
        if (actual.SlotId != expected.SlotId ||
            actual.SlotStartUtc != expected.SlotStartUtc ||
            actual.SlotEndUtc != expected.SlotEndUtc)
            throw new InvalidDataException(SlotMismatch);
        if (actual.MarketCaptureSessionId != expected.MarketCaptureSessionId ||
            actual.RequiredMarketSymbolSetSha256 != expected.RequiredMarketSymbolSetSha256 ||
            actual.MarketMappingContractVersion != expected.MarketMappingContractVersion ||
            actual.MarketMappingSetSha256 != expected.MarketMappingSetSha256)
            throw new InvalidDataException(MappingAuthorityMismatch);
        if (actual.CoreCommit != expected.CoreCommit ||
            actual.IntradayCommit != expected.IntradayCommit ||
            actual.MarketSelectionSha256 != expected.MarketSelectionSha256 ||
            actual.MarketManifestSha256 != expected.MarketManifestSha256)
            throw new InvalidDataException(ManifestBindingMismatch);
    }

    public static Arch7bEconomicRevisionInputBinding BindRevision(
        Arch7bPositionMarketSlotLineage lineage,
        PmsShadowIntradayEconomicProjection projection)
    {
        Validate(lineage);
        if (projection.SlotId != lineage.SlotId ||
            projection.SourceIngestionId != lineage.SourceIngestionId ||
            projection.SourceSessionId != lineage.SourceSessionId ||
            projection.PositionSnapshotId != lineage.SelectedPositionSnapshotId ||
            projection.PositionSnapshotAsOfUtc != lineage.PositionSnapshotAsOfUtc ||
            projection.MarketData.Count != RequiredPositionCount)
            throw new InvalidDataException(RevisionInputMismatch);
        var value = new Arch7bEconomicRevisionInputBinding(
            EconomicRevisionInputBindingVersion, lineage.EvidenceSha256,
            projection.ProjectionRevisionId, projection.InputSha256,
            projection.ManifestSha256, string.Empty);
        return value with { EvidenceSha256 = RevisionEvidenceSha256(value) };
    }

    public static void RequireArch7aRevision(
        Arch7bEconomicRevisionInputBinding binding,
        Guid arch7aEconomicRevisionId)
    {
        if (binding.ContractVersion != EconomicRevisionInputBindingVersion ||
            binding.EvidenceSha256 != RevisionEvidenceSha256(binding) ||
            binding.ProjectionRevisionId != arch7aEconomicRevisionId)
            throw new InvalidDataException(RevisionInputMismatch);
    }

    public static string MappingSetSha256(
        IEnumerable<PmsShadowEconomicMapping> mappings) =>
        Arch5bHashing.HashCanonical(mappings
            .OrderBy(value => value.InstrumentId)
            .Select(value => new
            {
                value.InstrumentId,
                value.VenueId,
                value.VenueInstrumentId,
                value.SecurityId,
                value.Symbol,
                value.LmaxInstrumentId,
                value.QuantityMultiplier,
                value.QuantityIncrement,
                value.PriceIncrement
            }).ToArray());

    public static string RequiredPmsUniverseSha256(
        IEnumerable<PmsShadowEconomicMapping> mappings) =>
        Arch5bHashing.HashCanonical(mappings
            .OrderBy(value => value.InstrumentId)
            .Select(value => new { value.InstrumentId, value.SecurityId, value.Symbol })
            .ToArray());

    public static string PositionLineSetSha256(PmsShadowEconomicSource source) =>
        Arch5bHashing.HashCanonical(source.Mappings
            .OrderBy(value => value.InstrumentId)
            .Select(value => new
            {
                value.InstrumentId,
                value.SecurityId,
                value.Symbol,
                CurrentBaseQuantity = source.CurrentPositions[value.InstrumentId]
            }).ToArray());

    public static string RequiredMarketSymbolSetSha256(IEnumerable<string> symbols) =>
        Arch5bHashing.HashCanonical(symbols.Order(StringComparer.Ordinal).ToArray());

    private static void RequireCommon(string version, string runId, string account,
        string targetProfile, string coreCommit, string intradayCommit, string slotId,
        DateTimeOffset slotStartUtc, DateTimeOffset slotEndUtc,
        string marketCaptureSessionId, bool noOrder)
    {
        if (version != Version || string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(targetProfile) ||
            string.IsNullOrWhiteSpace(marketCaptureSessionId) || !noOrder)
            throw new InvalidDataException(BindingRequired);
        if (!IsGitCommit(coreCommit) || !IsGitCommit(intradayCommit))
            throw new InvalidDataException(ManifestBindingMismatch);
        PmsShadowIntradayCadenceContract.RequireUtc(slotStartUtc);
        PmsShadowIntradayCadenceContract.RequireUtc(slotEndUtc);
        if (slotEndUtc - slotStartUtc != TimeSpan.FromMinutes(15) ||
            PmsShadowIntradayCadenceContract.WindowEnding(slotEndUtc).SlotId != slotId)
            throw new InvalidDataException(SlotMismatch);
    }

    private static void RequirePositionTime(DateTimeOffset positionAsOfUtc,
        DateTimeOffset slotStartUtc, long declaredAgeMilliseconds)
    {
        PmsShadowIntradayCadenceContract.RequireUtc(positionAsOfUtc);
        var actual = slotStartUtc - positionAsOfUtc;
        if (actual < TimeSpan.Zero)
            throw new InvalidDataException(PositionSnapshotMismatch);
        if (actual > TimeSpan.FromMilliseconds(MaximumPositionAgeMilliseconds))
            throw new InvalidDataException(PositionSnapshotMismatch);
        if (checked((long)actual.TotalMilliseconds) != declaredAgeMilliseconds)
            throw new InvalidDataException(PositionSnapshotMismatch);
    }

    private static void RequireSha(string value, string blocker)
    {
        if (!Arch5bHashing.IsSha256(value)) throw new InvalidDataException(blocker);
    }

    private static bool IsGitCommit(string value) => value.Length == 40 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string DraftEvidenceSha256(Arch7bPositionMarketSlotBindingDraft value) =>
        Arch5bHashing.HashCanonical(value with { EvidenceSha256 = string.Empty });
    private static string LineageEvidenceSha256(Arch7bPositionMarketSlotLineage value) =>
        Arch5bHashing.HashCanonical(value with { EvidenceSha256 = string.Empty });
    private static string RevisionEvidenceSha256(Arch7bEconomicRevisionInputBinding value) =>
        Arch5bHashing.HashCanonical(value with { EvidenceSha256 = string.Empty });
}

public sealed record Arch7bPositionMarketSlotBindingDraft(
    string ContractVersion, string RunId, string Account, string TargetProfile,
    string CoreCommit, string IntradayCommit,
    Guid SelectedPositionSnapshotId, DateTimeOffset PositionSnapshotAsOfUtc,
    string PositionSnapshotLineSetSha256, string PositionAuthority,
    Guid SourceIngestionId, string SourceSessionId, string RequiredPmsUniverseSha256,
    string SlotId, DateTimeOffset SlotStartUtc, DateTimeOffset SlotEndUtc,
    string MarketCaptureSessionId, string RequiredMarketSymbolSetSha256,
    string MarketMappingContractVersion, string MarketMappingSetSha256,
    long PositionSnapshotAgeAtSlotStartMilliseconds, bool NoOrder, string EvidenceSha256);

public sealed record Arch7bPositionMarketSlotLineage(
    string ContractVersion, string RunId, string Account, string TargetProfile,
    string CoreCommit, string IntradayCommit,
    Guid SelectedPositionSnapshotId, DateTimeOffset PositionSnapshotAsOfUtc,
    string PositionSnapshotLineSetSha256, string PositionAuthority,
    Guid SourceIngestionId, string SourceSessionId, string RequiredPmsUniverseSha256,
    string SlotId, DateTimeOffset SlotStartUtc, DateTimeOffset SlotEndUtc,
    string MarketCaptureSessionId, string RequiredMarketSymbolSetSha256,
    string MarketMappingContractVersion, string MarketMappingSetSha256,
    string ClockCaptureStartEvidenceSha256, string ClockPostCloseEvidenceSha256,
    string MarketSelectionSha256, string MarketManifestSha256,
    long PositionSnapshotAgeAtSlotStartMilliseconds, int MarketCoverageCount,
    DateTimeOffset SourceTimestampStartUtc, DateTimeOffset SourceTimestampEndUtc,
    string EconomicRevisionInputBindingVersion, bool NoOrder, string EvidenceSha256);

public sealed record Arch7bEconomicRevisionInputBinding(
    string ContractVersion, string PositionMarketLineageEvidenceSha256,
    Guid ProjectionRevisionId, string ProjectionInputSha256,
    string ProjectionManifestSha256, string EvidenceSha256);
