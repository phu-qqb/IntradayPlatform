using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class Arch7bPositionMarketReporting
{
    public static ReportingPositionMarketLineageFact Load(
        string lineagePath,
        string expectedLineageSha256,
        string revisionBindingPath,
        string expectedRevisionBindingSha256,
        Guid? latestEconomicRevisionId,
        Guid? latestArch7aRevisionId)
    {
        Arch7bPositionMarketSlotLineage lineage;
        try
        {
            lineage = Arch7bPositionMarketLineageFileStore.ReadLineage(
                lineagePath, expectedLineageSha256);
        }
        catch (InvalidDataException error) when (
            error.Message == Arch7bPositionMarketRuntimeContract.LineageNotInMarketManifest)
        {
            return Absent();
        }
        catch (FileNotFoundException)
        {
            return Absent();
        }
        catch (DirectoryNotFoundException)
        {
            return Absent();
        }
        catch (InvalidDataException)
        {
            return Contradictory();
        }

        Arch7bEconomicRevisionInputBinding binding;
        try
        {
            binding = Arch7bPositionMarketLineageFileStore.ReadRevisionBinding(
                revisionBindingPath, expectedRevisionBindingSha256);
        }
        catch (InvalidDataException error) when (
            error.Message == Arch7bPositionMarketRuntimeContract.RevisionBindingRequired)
        {
            return FromLineage(lineage, ReportingAuthority.Absent, null,
                ReportingAuthority.Absent, null);
        }
        catch (InvalidDataException)
        {
            return FromLineage(lineage, ReportingAuthority.Contradictory, null,
                ReportingAuthority.Contradictory, null);
        }

        if (binding.PositionMarketLineageEvidenceSha256 != lineage.EvidenceSha256)
            return FromLineage(lineage, ReportingAuthority.Contradictory,
                expectedRevisionBindingSha256, ReportingAuthority.Contradictory,
                binding.ProjectionRevisionId);

        var revisionStatus = latestEconomicRevisionId is null
            ? ReportingAuthority.Absent
            : latestEconomicRevisionId == binding.ProjectionRevisionId
                ? ReportingAuthority.Proven
                : ReportingAuthority.Contradictory;
        var arch7aStatus = latestArch7aRevisionId is null
            ? ReportingAuthority.Absent
            : latestArch7aRevisionId == binding.ProjectionRevisionId
                ? ReportingAuthority.Proven
                : ReportingAuthority.Contradictory;
        return FromLineage(lineage, revisionStatus,
            expectedRevisionBindingSha256, arch7aStatus,
            binding.ProjectionRevisionId);
    }

    public static ReportingPositionMarketLineageFact Absent() =>
        new(ReportingAuthority.Absent, null, null, null, null, null, null, null,
            ReportingAuthority.Absent, null, ReportingAuthority.Absent, null);

    private static ReportingPositionMarketLineageFact FromLineage(
        Arch7bPositionMarketSlotLineage lineage,
        string revisionStatus,
        string? revisionBindingSha256,
        string arch7aStatus,
        Guid? projectionRevisionId) =>
        new(ReportingAuthority.Proven, lineage.ContractVersion,
            lineage.EvidenceSha256, lineage.SelectedPositionSnapshotId,
            lineage.MarketCaptureSessionId, lineage.RequiredPmsUniverseSha256,
            lineage.RequiredMarketSymbolSetSha256, lineage.MarketMappingSetSha256,
            revisionStatus, revisionBindingSha256, arch7aStatus,
            projectionRevisionId);

    private static ReportingPositionMarketLineageFact Contradictory() =>
        new(ReportingAuthority.Contradictory, null, null, null, null, null, null,
            null, ReportingAuthority.Contradictory, null,
            ReportingAuthority.Contradictory, null);
}
