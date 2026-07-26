using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record PmsShadowEconomicProjectionIntegrityResult(
    Guid ProjectionRevisionId,
    string ContractVersion,
    string ProjectionIdentityContractVersion,
    string Status,
    string RecalculatedMarketDataSnapshotSha256,
    string RecalculatedTargetPositionsSha256,
    string RecalculatedDriftsSha256,
    string RecalculatedInputSha256,
    string RecalculatedManifestSha256,
    Guid RecalculatedProjectionRevisionId,
    IReadOnlyList<string> Blockers,
    string EvidenceSha256);

public static class PmsShadowEconomicProjectionIntegrityVerifier
{
    public const string ContractVersion = "pms_shadow_economic_projection_content_integrity_v2";
    public const string ProjectionIdentityV1 = "arch6f_economic_projection_identity_v1";
    public const string ProjectionIdentityV2 = "arch6f_economic_projection_identity_v2";
    public const string UnknownProjectionIdentity = "UNKNOWN";
    public const string Proven = "PROVEN";
    public const string Invalid = "INVALID";
    public const string MarketDataMismatch = "RPT2_MARKET_DATA_CONTENT_SHA_MISMATCH";
    public const string TargetPositionMismatch = "RPT2_TARGET_POSITION_CONTENT_SHA_MISMATCH";
    public const string DriftMismatch = "RPT2_DRIFT_CONTENT_SHA_MISMATCH";
    public const string InputMismatch = "RPT2_PROJECTION_INPUT_SHA_MISMATCH";
    public const string ManifestMismatch = "RPT2_PROJECTION_MANIFEST_SHA_MISMATCH";
    public const string IdentityMismatch = "RPT2_PROJECTION_IDENTITY_MISMATCH";

    public static PmsShadowEconomicProjectionIntegrityResult Verify(
        PmsShadowIntradayEconomicProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var marketSha = Arch5bHashing.HashCanonical(projection.MarketData
            .OrderBy(value => value.SecurityId, StringComparer.Ordinal)
            .ThenBy(value => value.InstrumentId)
            .ToArray());
        var targetSha = Arch5bHashing.HashCanonical(projection.TargetPositions
            .OrderBy(value => value.TargetPositionId)
            .ToArray());
        var driftSha = Arch5bHashing.HashCanonical(projection.PositionOnlyDrifts
            .OrderBy(value => value.DriftId)
            .ToArray());
        var marketId = projection.MarketDataSnapshotId;
        var inputSha = Arch5bHashing.HashCanonical(new
        {
            projection.SlotId,
            ArtifactSha256 = projection.RawCaptureSha256,
            marketId,
            MarketSha = marketSha,
            Models = projection.SelectedModelRuns
                .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
                .Select(value => new
                {
                    value.ModelRunId,
                    value.QubesInputSnapshotId,
                    value.OutputSha256
                }),
            projection.AccountSnapshotId,
            projection.PositionSnapshotId,
            PositionAsOfUtc = projection.PositionSnapshotAsOfUtc
        });
        var revisionIdentity =
            $"{PmsShadowIntradayEconomicContract.TestEnvironment}:{projection.SlotId}:{projection.RawCaptureSha256}:{PmsShadowIntradayEconomicContract.Version}";
        var legacyRevisionId = Arch5bHashing.GuidFromSha256(revisionIdentity);
        var currentRevisionId = Arch5bHashing.GuidFromSha256(projection.RevisionNumber == 1
            ? revisionIdentity
            : $"{revisionIdentity}:revision:{projection.RevisionNumber}:supersedes:{projection.SupersedesSlotManifestSha256}");
        var identityContractVersion = projection.ProjectionRevisionId == currentRevisionId
            ? projection.RevisionNumber == 1 ? ProjectionIdentityV1 : ProjectionIdentityV2
            : projection.RevisionNumber > 1 && projection.ProjectionRevisionId == legacyRevisionId
                ? ProjectionIdentityV1
                : UnknownProjectionIdentity;
        var identityProven = identityContractVersion != UnknownProjectionIdentity;
        var revisionId = identityProven ? projection.ProjectionRevisionId : currentRevisionId;
        var manifestSha = Arch5bHashing.HashCanonical(new
        {
            RevisionId = revisionId,
            Input = inputSha,
            Targets = targetSha,
            Drifts = driftSha,
            Supersedes = projection.SupersedesSlotManifestSha256,
            NoOrder = true,
            Blocker = PmsShadowStateContract.BrokerAdjustedBlocker
        });

        var blockers = new List<string>();
        AddMismatch(blockers, projection.MarketDataSnapshotSha256, marketSha, MarketDataMismatch);
        AddMismatch(blockers, projection.TargetPositionsSha256, targetSha, TargetPositionMismatch);
        AddMismatch(blockers, projection.DriftsSha256, driftSha, DriftMismatch);
        AddMismatch(blockers, projection.InputSha256, inputSha, InputMismatch);
        AddMismatch(blockers, projection.ManifestSha256, manifestSha, ManifestMismatch);
        if (!identityProven)
            blockers.Add(IdentityMismatch);

        var status = blockers.Count == 0 ? Proven : Invalid;
        var evidenceSha = Arch5bHashing.HashCanonical(new
        {
            ContractVersion,
            ProjectionIdentityContractVersion = identityContractVersion,
            projection.ProjectionRevisionId,
            Status = status,
            MarketDataSnapshotSha256 = marketSha,
            TargetPositionsSha256 = targetSha,
            DriftsSha256 = driftSha,
            InputSha256 = inputSha,
            ManifestSha256 = manifestSha,
            RecalculatedProjectionRevisionId = revisionId,
            Blockers = blockers
        });
        return new(
            projection.ProjectionRevisionId,
            ContractVersion,
            identityContractVersion,
            status,
            marketSha,
            targetSha,
            driftSha,
            inputSha,
            manifestSha,
            revisionId,
            blockers,
            evidenceSha);
    }

    private static void AddMismatch(
        ICollection<string> blockers,
        string stored,
        string calculated,
        string code)
    {
        if (!string.Equals(stored, calculated, StringComparison.Ordinal))
            blockers.Add(code);
    }
}
