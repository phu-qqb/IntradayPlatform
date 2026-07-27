using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPositionImportHardeningTests
{
    private static readonly DateTimeOffset Slot =
        new(2026, 7, 27, 11, 25, 0, TimeSpan.Zero);
    private static readonly Guid IngestionId = Id(1);
    private static readonly Guid AccountId = Id(2);

    [Fact]
    public void T51_LatestPreSlotSnapshotIsSelected()
    {
        var old = Snapshot(10, Slot.AddSeconds(-200));
        var latest = Snapshot(11, Slot.AddSeconds(-1));
        Assert.Equal(latest, Select(old, latest));
    }

    [Fact]
    public void T52_TieAtMaximumTimestampIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Select(Snapshot(10, Slot.AddSeconds(-1)),
                Snapshot(11, Slot.AddSeconds(-1))));
        Assert.Equal(PmsShadowPositionSnapshotForSlotSelection.Ambiguous,
            exception.Message);
    }

    [Fact]
    public void T53_AllSnapshotsAfterSlotAreRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Select(Snapshot(10, Slot.AddSeconds(1))));
        Assert.Equal(PmsShadowPositionSnapshotForSlotSelection.AfterSlot,
            exception.Message);
    }

    [Fact]
    public void T54_StaleSnapshotIsRejectedAt301Seconds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Select(Snapshot(10, Slot.AddSeconds(-301))));
        Assert.Equal(PmsShadowPositionSnapshotForSlotSelection.Stale,
            exception.Message);
    }

    [Fact]
    public void T55_MissingSnapshotIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Select());
        Assert.Equal(PmsShadowPositionSnapshotForSlotSelection.Missing,
            exception.Message);
    }

    [Fact]
    public void T56_SlotPredatingImportedSnapshotDoesNotSelectIt()
    {
        var old = Snapshot(10, Slot.AddSeconds(-1));
        var imported = Snapshot(11, Slot.AddSeconds(1));
        Assert.Equal(old, Select(old, imported));
    }

    [Fact]
    public void T57_InvalidLatestNeverFallsBackToOlderSnapshot()
    {
        var old = Snapshot(10, Slot.AddSeconds(-2));
        var latest = Snapshot(11, Slot.AddSeconds(-1)) with
        {
            Classification = PmsShadowStateContract.EvidenceClassification
        };
        var selected = Select(old, latest);
        Assert.Equal(latest.PositionSnapshotId, selected.PositionSnapshotId);
        Assert.Throws<InvalidDataException>(() =>
            Validate(selected, Lines(selected), Mappings()));
    }

    [Fact]
    public void T58_BracketedSnapshotWith99LinesIsAccepted()
    {
        var selected = Snapshot(11, Slot.AddSeconds(-1));
        Validate(selected, Lines(selected), Mappings());
    }

    [Fact]
    public void T59_BrokerAuthorityBooleanAloneIsInsufficient()
    {
        var selected = Snapshot(11, Slot.AddSeconds(-1)) with
        {
            Classification = PmsShadowStateContract.EvidenceClassification
        };
        Assert.True(selected.BrokerAuthority);
        Assert.Throws<InvalidDataException>(() =>
            Validate(selected, Lines(selected), Mappings()));
    }

    [Fact]
    public void T60_SparseSnapshotIsRejected()
    {
        var selected = Snapshot(11, Slot.AddSeconds(-1));
        Assert.Throws<InvalidDataException>(() =>
            Validate(selected, Lines(selected).Take(3).ToArray(), Mappings()));
    }

    [Fact]
    public void T61_MappingIdentityMismatchIsRejected()
    {
        var selected = Snapshot(11, Slot.AddSeconds(-1));
        var mappings = Mappings().ToArray();
        mappings[0] = mappings[0] with { SecurityId = "CHANGED" };
        Assert.Throws<InvalidDataException>(() =>
            Validate(selected, Lines(selected), mappings));
    }

    [Fact]
    public void T62_ExactPrivilegeSetIsAccepted()
    {
        Arch7bPositionImportPrivilegePolicy.RequireExact(
            Privileges());
    }

    [Fact]
    public void T63_UpdatePrivilegeIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportPrivilegePolicy.RequireExact(
                Privileges() with { PositionSnapshotUpdate = true }));
        Assert.Equal("ARCH7B_POSITION_IMPORT_UPDATE_PRIVILEGE_FORBIDDEN",
            exception.Message);
    }

    [Fact]
    public void T64_DeletePrivilegeIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportPrivilegePolicy.RequireExact(
                Privileges() with { PositionSnapshotLineDelete = true }));
    }

    [Fact]
    public void T65_OrderFillOrLedgerInsertPrivilegeIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportPrivilegePolicy.RequireExact(
                Privileges() with { ForbiddenInsert = true }));
    }

    [Fact]
    public void T66_ArmedStateEvidenceIsDeterministic()
    {
        var package = Arch7bAppendOnlyPositionImportTests.Package();
        var first = Armed(package);
        var second = Armed(package);
        Assert.Equal(first, second);
        Assert.True(Arch5bHashing.IsSha256(first.EvidenceSha256));
    }

    [Fact]
    public void T67_ReadyMarkerBindsPackageAndArmedEvidence()
    {
        var package = Arch7bAppendOnlyPositionImportTests.Package();
        var armed = Armed(package);
        var marker = Arch7bPositionImportReadyMarkerStore.Create(
            armed, package, Target(), Repository(),
            package.Snapshot.PositionReportP2Utc);
        Assert.Equal(package.ManifestSha256, marker.PackageManifestSha256);
        Assert.Equal(armed.EvidenceSha256, marker.ArmedEvidenceSha256);
    }

    [Fact]
    public void T68_ArmAfterBracketIsRejected()
    {
        var package = Arch7bAppendOnlyPositionImportTests.Package();
        var armed = Arch7bPositionImportArmedStateStore.Create(
            Target(), Repository(), "authorization", "owner",
            package.Universe.SourceIngestionId,
            package.Snapshot.BracketLowerBoundUtc.AddSeconds(1));
        var marker = Arch7bPositionImportReadyMarkerStore.Create(
            armed, package, Target(), Repository(),
            package.Snapshot.PositionReportP2Utc);
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportReadyMarkerStore.Validate(
                marker, armed, package, Target(), Repository(),
                "authorization", "owner",
                package.Snapshot.PositionReportP2Utc.AddSeconds(1)));
        Assert.Equal(Arch7bPositionImportContract.ChronologyInvalid,
            exception.Message);
    }

    [Fact]
    public void T69_OwnerMismatchIsRejectedExplicitly()
    {
        var package = Arch7bAppendOnlyPositionImportTests.Package();
        var armed = Armed(package);
        var marker = Arch7bPositionImportReadyMarkerStore.Create(
            armed, package, Target(), Repository(),
            package.Snapshot.PositionReportP2Utc);
        var exception = Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportReadyMarkerStore.Validate(
                marker, armed, package, Target(), Repository(),
                "authorization", "other-owner",
                package.Snapshot.PositionReportP2Utc.AddSeconds(1)));
        Assert.Equal(Arch7bPositionImportContract.OwnerMismatch,
            exception.Message);
    }

    [Fact]
    public void T70_ExtraPackageFileIsRejected()
    {
        var root = Arch7bAppendOnlyPositionImportTests.WritePackageFixture();
        try
        {
            File.WriteAllText(Path.Combine(root, "extra.txt"), "extra");
            var exception = Assert.Throws<InvalidDataException>(() =>
                Arch7bPositionImportPackageReader.Read(root));
            Assert.Equal(
                "ARCH7B_POSITION_IMPORT_PACKAGE_INVENTORY_MISMATCH",
                exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T71_CsvQuantityMismatchIsRejectedAfterManifestRehash()
    {
        var root = Arch7bAppendOnlyPositionImportTests.WritePackageFixture();
        try
        {
            var csvPath = Path.Combine(root, "normalized-position-lines.csv");
            var rows = File.ReadAllLines(csvPath);
            var columns = rows[1].Split(',');
            columns[9] = "1";
            rows[1] = string.Join(',', columns);
            File.WriteAllLines(csvPath, rows);
            RehashManifestFile(root, "normalized-position-lines.csv");
            var exception = Assert.Throws<InvalidDataException>(() =>
                Arch7bPositionImportPackageReader.Read(root));
            Assert.Equal(
                Arch7bPositionImportPackageIntegrity.CsvJsonMismatch,
                exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T72_DatabaseUniverseMismatchIsRejected()
    {
        var package = Arch7bAppendOnlyPositionImportTests.Package();
        var changed = package.Universe with
        {
            RequiredUniverseSha256 = Hash('9')
        };
        Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportUniverseValidator.RequireExact(
                package.Universe, changed, Target()));
    }

    private static PmsShadowPositionSnapshotRow Select(
        params PmsShadowPositionSnapshotRow[] snapshots) =>
        PmsShadowPositionSnapshotForSlotSelection.Select(snapshots, Slot);

    private static PmsShadowPositionSnapshotRow Snapshot(
        int id, DateTimeOffset asOfUtc) =>
        new(Id(id), IngestionId, AccountId,
            DateOnly.FromDateTime(asOfUtc.UtcDateTime), asOfUtc,
            Hash('a'), true, false, true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);

    private static void Validate(
        PmsShadowPositionSnapshotRow snapshot,
        IReadOnlyList<PmsShadowPositionSnapshotLineRow> lines,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappings) =>
        PmsShadowPositionSnapshotForSlotSelection.ValidateSelected(
            snapshot,
            new(IngestionId, "gate", "session", Hash('b'),
                PmsShadowIngestionStatuses.Completed,
                Slot.AddDays(-1), Slot.AddDays(-1), "contract", "TEST",
                "classification", Hash('c')),
            new(AccountId, IngestionId, "demo", "GLOBAL", "USD",
                1_000_000m, DateOnly.FromDateTime(Slot.UtcDateTime),
                Slot, "authority", Hash('d'), Hash('e'), "classification"),
            lines, mappings);

    private static IReadOnlyList<PmsShadowPositionSnapshotLineRow> Lines(
        PmsShadowPositionSnapshotRow snapshot) =>
        Enumerable.Range(1, 99).Select(index =>
            new PmsShadowPositionSnapshotLineRow(
                snapshot.PositionSnapshotId, Id(1000 + index),
                $"SEC{index:D3}", $"AAA{index:D3}", 0m)).ToArray();

    private static IReadOnlyList<PmsShadowSecurityMappingRow> Mappings() =>
        Enumerable.Range(1, 99).Select(index =>
            new PmsShadowSecurityMappingRow(
                IngestionId, Id(1000 + index), Id(2000 + index),
                Id(3000 + index), $"SEC{index:D3}", $"AAA{index:D3}",
                $"LMAX{index:D3}", 1m, 1m, 0.00001m, Hash('f')))
            .ToArray();

    private static Arch7bPositionImportPrivilegeState Privileges() =>
        new(true, true, true, false, false, false, false, false);

    private static Arch7bPositionImportArmedState Armed(
        Arch7bPositionImportPackage package) =>
        Arch7bPositionImportArmedStateStore.Create(
            Target(), Repository(), "authorization", "owner",
            package.Universe.SourceIngestionId,
            package.Snapshot.BracketLowerBoundUtc.AddSeconds(-1));

    private static PmsShadowPostgreSqlTarget Target() =>
        Arch7bAppendOnlyPositionImportTests.Target();

    private static Arch7bRepositoryState Repository() =>
        new(GitArch7bRepositoryStateAuthority.ContractVersion,
            "C:\\repo", Hash('a', 40), Hash('a', 40), true, true);

    private static void RehashManifestFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        var manifestPath = Path.Combine(root, "manifest.json");
        var document = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        document["files"]![name] =
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        File.WriteAllText(manifestPath, document.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Guid Id(int value) =>
        new(value, 0, 0, new byte[8]);

    private static string Hash(char value, int length = 64) =>
        new(value, length);
}
