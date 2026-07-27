using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bAppendOnlyPositionImportTests
{
    private static readonly DateTimeOffset P2 =
        new(2026, 7, 27, 11, 23, 45, TimeSpan.Zero);

    [Fact]
    public void T01_ExistingSchemaSupportsAdditionalPositionSnapshot()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(PmsShadowPositionSnapshotRow))!;
        Assert.Equal("position_snapshots", entity.GetTableName());
        Assert.Single(entity.GetKeys());
    }

    [Fact]
    public void T02_PositionSnapshotForeignKeysRemainExact()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(PmsShadowPositionSnapshotRow))!;
        Assert.Contains(entity.GetForeignKeys(), value =>
            value.Properties.Single().Name == nameof(PmsShadowPositionSnapshotRow.IngestionId));
        Assert.Contains(entity.GetForeignKeys(), value =>
            value.Properties.Single().Name == nameof(PmsShadowPositionSnapshotRow.AccountSnapshotId));
    }

    [Fact]
    public void T03_PositionSnapshotIdempotencyConstraintIsExistingIndex()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(PmsShadowPositionSnapshotRow))!;
        Assert.Contains(entity.GetIndexes(), value => value.IsUnique &&
            value.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(PmsShadowPositionSnapshotRow.IngestionId),
                 nameof(PmsShadowPositionSnapshotRow.SnapshotSha256)]));
    }

    [Fact]
    public void T04_PositionLineKeyIsSnapshotAndInstrument()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(
            typeof(PmsShadowPositionSnapshotLineRow))!;
        Assert.Equal(
            [nameof(PmsShadowPositionSnapshotLineRow.PositionSnapshotId),
             nameof(PmsShadowPositionSnapshotLineRow.InstrumentId)],
            entity.FindPrimaryKey()!.Properties.Select(value => value.Name));
    }

    [Fact]
    public void T05_NoMigrationIsAddedToContract()
    {
        Assert.Equal("20260723085240_AddArch7bLmaxDemoKnownOrderLifecycle",
            PmsShadowStateContract.MigrationIds[^1]);
    }

    [Fact]
    public void T06_CanonicalFreshnessIsFiveMinutes()
    {
        Assert.Equal(PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds,
            Arch7bPositionImportContract.MaximumAgeSeconds);
        Assert.Equal(300, Arch7bPositionImportContract.MaximumAgeSeconds);
    }

    [Fact]
    public void T07_FreshBracketIsEligible()
    {
        var result = Freshness(P2.AddSeconds(299));
        Assert.True(result.ApplyEligible);
        Assert.Equal(Arch7bPositionImportContract.Eligible, result.Status);
    }

    [Fact]
    public void T08_ExactFreshnessBoundaryIsEligible()
    {
        Assert.True(Freshness(P2.AddSeconds(300)).ApplyEligible);
    }

    [Fact]
    public void T09_StaleBracketIsRejected()
    {
        var result = Freshness(P2.AddSeconds(301));
        Assert.False(result.ApplyEligible);
        Assert.Equal(Arch7bPositionImportContract.Stale, result.Status);
    }

    [Fact]
    public void T10_FutureBracketIsRejected()
    {
        Assert.Equal(Arch7bPositionImportContract.FromFuture,
            Freshness(P2.AddSeconds(-1)).Status);
    }

    [Fact]
    public void T11_BracketPredatingPmsSourceIsRejected()
    {
        var package = Package(ingestionCompletedAtUtc: P2.AddSeconds(1));
        var result = Arch7bPositionImportFreshnessPolicy.Evaluate(
            package, P2.AddSeconds(2), false);
        Assert.Equal(Arch7bPositionImportContract.PredatesPmsSource, result.Status);
    }

    [Fact]
    public void T12_HistoricalFixtureIsNeverApplyEligible()
    {
        var result = Arch7bPositionImportFreshnessPolicy.Evaluate(
            Package(), P2.AddDays(1), true);
        Assert.False(result.ApplyEligible);
        Assert.Equal(Arch7bPositionImportContract.HistoricalFixture, result.Status);
    }

    [Fact]
    public void T13_NewPlanAddsOneSnapshot()
    {
        var plan = Plan(State());
        Assert.Equal(Arch7bPositionImportContract.New, plan.Status);
        Assert.Equal(1, plan.PositionSnapshotRowsToAdd);
    }

    [Fact]
    public void T14_NewPlanAddsExactlyNinetyNineLines()
    {
        Assert.Equal(99, Plan(State()).PositionSnapshotLineRowsToAdd);
    }

    [Fact]
    public void T15_NewPlanDoesNotAddAccountSnapshot()
    {
        Assert.Equal(0, Plan(State()).AccountSnapshotsToAdd);
    }

    [Fact]
    public void T16_NewPlanDoesNotMutateIngestion()
    {
        Assert.False(Plan(State()).SourceIngestionMutationRequired);
    }

    [Fact]
    public void T17_NewPlanDoesNotDuplicateModels()
    {
        Assert.Equal(0, Plan(State()).ModelRunsToAdd);
    }

    [Fact]
    public void T18_NewPlanDoesNotDuplicateWeights()
    {
        Assert.Equal(0, Plan(State()).TargetWeightsToAdd);
    }

    [Fact]
    public void T19_NewPlanDoesNotDuplicateMappings()
    {
        Assert.Equal(0, Plan(State()).SecurityMappingsToAdd);
    }

    [Fact]
    public void T20_NewPlanPreservesNoOrder()
    {
        var plan = Plan(State());
        Assert.True(plan.NoOrder);
        Assert.True(plan.NoFix);
        Assert.True(plan.NoFill);
        Assert.True(plan.NoPositionLedgerEvent);
    }

    [Fact]
    public void T21_IdenticalReplayAddsNoRows()
    {
        var package = Package();
        var existing = ExpectedRow(package);
        var lines = ExpectedLines(package);
        var plan = Plan(State(existingById: existing, existingLines: lines), package);
        Assert.Equal(Arch7bPositionImportContract.AlreadyAppliedIdentical, plan.Status);
        Assert.Equal(0, plan.PositionSnapshotRowsToAdd);
        Assert.Equal(0, plan.PositionSnapshotLineRowsToAdd);
    }

    [Fact]
    public void T22_IdenticalEvidenceReplayAddsNoRows()
    {
        var package = Package();
        var plan = Plan(State(existingByEvidence: ExpectedRow(package),
            existingLines: ExpectedLines(package)), package);
        Assert.Equal(Arch7bPositionImportContract.AlreadyAppliedIdentical, plan.Status);
    }

    [Fact]
    public void T23_SameIdDifferentEvidenceConflicts()
    {
        var package = Package();
        var changed = ExpectedRow(package) with { SnapshotSha256 = Sha('9') };
        Assert.Equal(Arch7bPositionImportContract.Conflict,
            Plan(State(existingById: changed), package).Status);
    }

    [Fact]
    public void T24_SameEvidenceDifferentHeaderConflicts()
    {
        var package = Package();
        var changed = ExpectedRow(package) with { BrokerAuthority = false };
        Assert.Equal(Arch7bPositionImportContract.Conflict,
            Plan(State(existingByEvidence: changed), package).Status);
    }

    [Fact]
    public void T25_MissingSourceIngestionFailsClosed()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Plan(State(sourceIngestionExists: false)));
        Assert.Equal("ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISSING",
            exception.Message);
    }

    [Fact]
    public void T26_MissingSourceAccountFailsClosed()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Plan(State(sourceAccountExists: false)));
        Assert.Equal("ARCH7B_POSITION_IMPORT_SOURCE_ACCOUNT_MISSING",
            exception.Message);
    }

    [Fact]
    public void T27_PendingModelChangesFailClosed()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Plan(State(pending: true)));
        Assert.Equal("ARCH7B_POSITION_IMPORT_PENDING_MODEL_CHANGES",
            exception.Message);
    }

    [Fact]
    public void T28_LineageUsesExistingAccountSnapshot()
    {
        var package = Package();
        Assert.Equal(package.Universe.SourceAccountSnapshotId,
            Plan(State(), package).SourceAccountSnapshotId);
        Assert.NotEqual(package.Snapshot.AccountSnapshotId,
            package.Universe.SourceAccountSnapshotId);
    }

    [Fact]
    public void T29_LineageUsesExistingIngestion()
    {
        var package = Package();
        Assert.Equal(package.Universe.SourceIngestionId,
            Plan(State(), package).SourceIngestionId);
    }

    [Fact]
    public void T30_SnapshotShaBindsConsumerEvidence()
    {
        var package = Package();
        Assert.Equal(package.Snapshot.EvidenceSha256,
            Plan(State(), package).PositionSnapshotSha256);
    }

    [Fact]
    public void T31_AllCandidateLinesAreExplicitZero()
    {
        var package = Package();
        Assert.Equal(99, package.Snapshot.Lines.Count);
        Assert.All(package.Snapshot.Lines,
            value => Assert.Equal(0m, value.CurrentBaseQuantity));
    }

    [Fact]
    public void T32_AllCandidateLinesBindTheSnapshot()
    {
        var package = Package();
        Assert.All(package.Snapshot.Lines, value =>
            Assert.Equal(package.Snapshot.PositionSnapshotId,
                value.PositionSnapshotId));
    }

    [Fact]
    public void T33_AllCandidateLinesBindTheSourceIngestion()
    {
        var package = Package();
        Assert.All(package.Snapshot.Lines, value =>
            Assert.Equal(package.Universe.SourceIngestionId,
                value.SourceIngestionId));
    }

    [Fact]
    public void T34_NormalizedLineSetHashIsDeterministic()
    {
        var package = Package();
        Assert.Equal(package.Snapshot.NormalizedLineSetSha256,
            Arch5bHashing.HashCanonical(package.Snapshot.Lines));
    }

    [Fact]
    public void T35_ReadyMarkerValidatesAllBindings()
    {
        var package = Package();
        ValidateMarker(Marker(package), package);
    }

    [Fact]
    public void T36_MissingFutureAuthorizationFailsClosed()
    {
        var package = Package();
        var marker = Marker(package) with { FutureAuthorizationId = "" };
        var exception = Assert.Throws<InvalidDataException>(() =>
            ValidateMarker(marker, package));
        Assert.Equal(Arch7bPositionImportContract.AuthorizationMismatch, exception.Message);
    }

    [Fact]
    public void T37_TargetFingerprintMismatchFailsClosed()
    {
        var package = Package();
        var marker = Marker(package) with { TargetFingerprint = Sha('0') };
        Assert.Throws<InvalidDataException>(() =>
            ValidateMarker(marker, package));
    }

    [Fact]
    public void T38_RepositoryCommitMismatchFailsClosed()
    {
        var package = Package();
        Assert.Throws<InvalidDataException>(() =>
            Arch7bPositionImportReadyMarkerStore.Validate(
                Marker(package), Armed(package), package, Target(),
                Repository() with { HeadCommit = Sha('b', 40) },
                "authorization", "owner",
                P2.AddSeconds(1)));
    }

    [Fact]
    public void T39_EvidenceMismatchFailsClosed()
    {
        var package = Package();
        var marker = Marker(package) with
        {
            ConsumerSnapshotEvidenceSha256 = Sha('1')
        };
        Assert.Throws<InvalidDataException>(() =>
            ValidateMarker(marker, package));
    }

    [Fact]
    public void T40_ReadyMarkerPublishesAtomically()
    {
        var root = Temp();
        try
        {
            var path = Path.Combine(root, "ready.json");
            Arch7bPositionImportReadyMarkerStore.PublishAtomic(
                path, Marker(Package()));
            Assert.True(File.Exists(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T41_ReadyMarkerCannotOverwrite()
    {
        var root = Temp();
        try
        {
            var path = Path.Combine(root, "ready.json");
            Arch7bPositionImportReadyMarkerStore.PublishAtomic(
                path, Marker(Package()));
            Assert.Throws<IOException>(() =>
                Arch7bPositionImportReadyMarkerStore.PublishAtomic(
                    path, Marker(Package())));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T42_ConcurrentOwnerIsRejected()
    {
        var root = Temp();
        try
        {
            var path = Path.Combine(root, "owner.lock");
            using var first =
                Arch7bPositionImportReadyMarkerStore.AcquireOwner(path, "owner-a");
            var exception = Assert.Throws<InvalidDataException>(() =>
                Arch7bPositionImportReadyMarkerStore.AcquireOwner(path, "owner-b"));
            Assert.Equal("ARCH7B_POSITION_IMPORT_OWNER_ALREADY_ACQUIRED",
                exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T43_OwnerLockIsRemovedOnDispose()
    {
        var root = Temp();
        try
        {
            var path = Path.Combine(root, "owner.lock");
            using (Arch7bPositionImportReadyMarkerStore.AcquireOwner(path, "owner"))
                Assert.True(File.Exists(path));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T44_PackageReaderAcceptsCompleteDeterministicFixture()
    {
        var root = WritePackageFixture();
        try
        {
            var package = Arch7bPositionImportPackageReader.Read(root);
            Assert.Equal(99, package.Snapshot.Lines.Count);
            Assert.Equal(Sha('e'), package.Snapshot.EvidenceSha256);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T45_PackageReaderRejectsTamperedCsv()
    {
        var root = WritePackageFixture();
        try
        {
            File.AppendAllText(Path.Combine(root, "normalized-position-lines.csv"),
                "tampered\n");
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPositionImportPackageReader.Read(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T46_PackageReaderRejectsMissingFile()
    {
        var root = WritePackageFixture();
        try
        {
            File.Delete(Path.Combine(root,
                "pms-bracketed-global-flat-position-snapshot.json"));
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPositionImportPackageReader.Read(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void T47_DryRunWritesExactlyEightEvidenceFiles()
    {
        var root = Path.Combine(Temp(), "output");
        var package = Package();
        try
        {
            Arch7bPositionImportOutputWriter.Write(
                root, package,
                Arch7bPositionImportFreshnessPolicy.Evaluate(
                    package, P2.AddDays(1), true),
                Plan(State(), package), Target(), Sha('a', 40));
            Assert.Equal(8, Directory.GetFiles(root).Length);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, true);
        }
    }

    [Fact]
    public void T48_DryRunManifestDeclaresNoDatabaseWrite()
    {
        var root = Path.Combine(Temp(), "output");
        var package = Package();
        try
        {
            Arch7bPositionImportOutputWriter.Write(
                root, package,
                Arch7bPositionImportFreshnessPolicy.Evaluate(
                    package, P2.AddDays(1), true),
                Plan(State(), package), Target(), Sha('a', 40));
            using var document = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(root, "manifest.json")));
            Assert.True(document.RootElement.GetProperty(
                "no_database_write").GetBoolean());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, true);
        }
    }

    [Fact]
    public void T49_DryRunDeclaresNoFixOrderFillOrLedger()
    {
        var plan = Plan(State());
        Assert.True(plan.NoFix && plan.NoOrder &&
                    plan.NoFill && plan.NoPositionLedgerEvent);
    }

    [Fact]
    public void T50_HistoricalDryRunReportsExactBlocker()
    {
        var package = Package();
        var freshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
            package, P2.AddDays(1), true);
        var plan = Arch7bPositionImportPlanner.Build(
            package, freshness, State());
        Assert.Equal(
            Arch7bPositionImportContract.HistoricalFixture,
            plan.ImportEligibility);
    }

    private static PmsShadowDbContext Context() =>
        new(new DbContextOptionsBuilder<PmsShadowDbContext>()
            .UseNpgsql("Host=localhost;Database=qq_test;Username=test;Password=test")
            .Options);

    private static Arch7bPositionImportFreshness Freshness(
        DateTimeOffset observedUtc) =>
        Arch7bPositionImportFreshnessPolicy.Evaluate(
            Package(), observedUtc, false);

    private static Arch7bPositionImportPlan Plan(
        Arch7bPositionImportDatabaseState state,
        Arch7bPositionImportPackage? package = null)
    {
        package ??= Package();
        return Arch7bPositionImportPlanner.Build(
            package,
            new(Arch7bPositionImportContract.HistoricalFixture, false, 300, 1),
            state);
    }

    private static Arch7bPositionImportDatabaseState State(
        bool sourceIngestionExists = true,
        bool sourceAccountExists = true,
        bool pending = false,
        PmsShadowPositionSnapshotRow? existingById = null,
        PmsShadowPositionSnapshotRow? existingByEvidence = null,
        IReadOnlyList<PmsShadowPositionSnapshotLineRow>? existingLines = null) =>
        new(sourceIngestionExists, sourceAccountExists, 4, 288, 99,
            existingById, existingByEvidence, existingLines ?? [],
            7, 693, true, pending);

    private static PmsShadowPositionSnapshotRow ExpectedRow(
        Arch7bPositionImportPackage package) =>
        new(package.Snapshot.PositionSnapshotId,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            DateOnly.FromDateTime(P2.UtcDateTime),
            P2,
            package.Snapshot.EvidenceSha256,
            true, false, true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);

    private static IReadOnlyList<PmsShadowPositionSnapshotLineRow> ExpectedLines(
        Arch7bPositionImportPackage package) =>
        package.Snapshot.Lines.Select(value =>
            new PmsShadowPositionSnapshotLineRow(
                value.PositionSnapshotId, value.InstrumentId,
                value.SecurityId, value.Symbol, value.CurrentBaseQuantity)).ToArray();

    internal static Arch7bPositionImportPackage Package(
        DateTimeOffset? ingestionCompletedAtUtc = null)
    {
        var ingestionId = Id(1);
        var sourceAccountId = Id(2);
        var snapshotId = Id(3);
        var instruments = Enumerable.Range(1, 99).Select(index =>
            new Arch7bRequiredInstrument(Id(1000 + index),
                $"SEC{index:D3}", $"SYM{index:D3}", $"LMAX{index:D3}", Sha('a')))
            .ToArray();
        var universe = new Arch7bRequiredPmsUniverse(
            ingestionId, "arch6b-source", sourceAccountId, 1_000_000m,
            ingestionCompletedAtUtc ?? P2.AddDays(-1),
            [], P2.AddDays(-1), P2.AddDays(-1),
            P2.AddDays(-1), P2.AddDays(-1),
            [], [], instruments,
            new(99, 99, 0, 0, "ONE_TO_ONE"),
            new Dictionary<string, int>
            {
                ["INFX7"] = 66,
                ["INFX8"] = 66,
                ["INFX9"] = 78,
                ["INFX10"] = 78
            },
            Sha('u'), Arch7bBracketedGlobalFlatContract.SourceSelectionAuthority,
            Arch7bBracketedGlobalFlatContract.TargetProfile, Sha('f'),
            true, false, true);
        var lines = instruments.Select((value, index) =>
            new Arch7bNormalizedPositionLine(
                Id(2000 + index), snapshotId, value.InstrumentId,
                value.SecurityId, value.Symbol, value.LmaxInstrumentId,
                value.MappingSha256, ingestionId, universe.SourceSessionId,
                0m, Arch7bBracketedGlobalFlatContract.ProvenanceKind,
                Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
                Arch7bBracketedGlobalFlatContract.AccountId, 0, Sha('b'),
                universe.RequiredUniverseSha256, Sha('c', 40), P2, Sha('d')))
            .ToArray();
        var lineSet = Arch5bHashing.HashCanonical(lines);
        var snapshot = new Arch7bPmsGlobalFlatPositionSnapshot(
            Arch7bBracketedGlobalFlatContract.Version,
            Id(4), snapshotId,
            Arch7bBracketedGlobalFlatContract.AccountId,
            Arch7bBracketedGlobalFlatContract.Environment,
            Sha('c', 40),
            Arch7bBracketedGlobalFlatContract.CoreContractVersion,
            Sha('b'), P2.AddSeconds(-10), P2, P2.AddSeconds(10), P2,
            universe.IngestionCompletedAtUtc,
            P2.AddDays(-1), P2.AddMinutes(15), true,
            "PROVEN_BROKER_SNAPSHOT_AFTER_PMS_INGESTION_AND_MODEL_ASOF",
            Arch7bBracketedGlobalFlatContract.TemporalLineageContractVersion,
            Arch7bBracketedGlobalFlatContract.TargetCloseTemporalContract,
            Arch7bBracketedGlobalFlatContract.ImportEligibility,
            Arch7bBracketedGlobalFlatContract.ImportFreshnessStatus,
            universe.MappingCardinalities,
            0, 99, 99, 99, 0,
            universe.RequiredUniverseSha256, lineSet,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
            Arch7bBracketedGlobalFlatContract.WorkingOrderAuthority,
            Arch7bBracketedGlobalFlatContract.WorkingOrderBlocker,
            false, true, true, true, lines, Sha('e'));
        return new("fixture", Sha('m'), universe, snapshot);
    }

    private static void ValidateMarker(
        Arch7bPositionImportReadyMarker marker,
        Arch7bPositionImportPackage package) =>
        Arch7bPositionImportReadyMarkerStore.Validate(
            marker, Armed(package), package, Target(), Repository(),
            "authorization", "owner", P2.AddSeconds(1));

    private static Arch7bPositionImportReadyMarker Marker(
        Arch7bPositionImportPackage package) =>
        Arch7bPositionImportReadyMarkerStore.Create(
            Armed(package), package, Target(), Repository(), P2);

    private static Arch7bPositionImportArmedState Armed(
        Arch7bPositionImportPackage package) =>
        Arch7bPositionImportArmedStateStore.Create(
            Target(), Repository(), "authorization", "owner",
            package.Universe.SourceIngestionId,
            package.Snapshot.BracketLowerBoundUtc.AddSeconds(-1));

    private static Arch7bRepositoryState Repository() =>
        new(GitArch7bRepositoryStateAuthority.ContractVersion,
            "C:\\repo", Sha('a', 40), Sha('a', 40), true, true);

    internal static PmsShadowPostgreSqlTarget Target() =>
        new("test.example", 5432,
            Arch7bBracketedGlobalFlatContract.TargetDatabase,
            Arch7bBracketedGlobalFlatContract.TargetEnvironment,
            PmsShadowStateContract.SchemaName,
            Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
            Arch7bBracketedGlobalFlatContract.TargetProfile,
            PmsShadowPostgreSqlTargetContract.RemoteTlsKind,
            "VERIFYFULL", Sha('f'));

    internal static string WritePackageFixture()
    {
        var root = Temp();
        var package = Package();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        File.WriteAllBytes(Path.Combine(root, "required-pms-universe.json"),
            JsonSerializer.SerializeToUtf8Bytes(package.Universe, json));
        File.WriteAllBytes(Path.Combine(root,
                "pms-bracketed-global-flat-position-snapshot.json"),
            JsonSerializer.SerializeToUtf8Bytes(package.Snapshot, json));
        var csv = new StringBuilder("""
            position_snapshot_line_id,position_snapshot_id,instrument_id,security_id,symbol,lmax_instrument_id,mapping_sha256,source_ingestion_id,pms_source_session_id,current_base_quantity,provenance_kind,position_authority_code,account_id,broker_position_count,bracket_evidence_sha256,required_universe_sha256,core_repository_commit,position_snapshot_as_of_utc,evidence_sha256

            """);
        foreach (var line in package.Snapshot.Lines)
            csv.AppendJoin(',', new[]
            {
                line.PositionSnapshotLineId.ToString("D"),
                line.PositionSnapshotId.ToString("D"),
                line.InstrumentId.ToString("D"),
                line.SecurityId,
                line.Symbol,
                line.LmaxInstrumentId,
                line.MappingSha256,
                line.SourceIngestionId.ToString("D"),
                line.PmsSourceSessionId,
                line.CurrentBaseQuantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                line.ProvenanceKind,
                line.PositionAuthorityCode,
                line.AccountId,
                line.BrokerPositionCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                line.BracketEvidenceSha256,
                line.RequiredUniverseSha256,
                line.CoreRepositoryCommit,
                line.PositionSnapshotAsOfUtc.ToString(
                    "O", System.Globalization.CultureInfo.InvariantCulture),
                line.EvidenceSha256
            }).Append('\n');
        File.WriteAllText(Path.Combine(root, "normalized-position-lines.csv"),
            csv.ToString(), new UTF8Encoding(false));
        var files = Directory.GetFiles(root).ToDictionary(
            path => Path.GetFileName(path),
            path => Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(path))));
        File.WriteAllBytes(Path.Combine(root, "manifest.json"),
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                contract_version = Arch7bBracketedGlobalFlatContract.Version,
                no_order = true,
                no_fix = true,
                no_database_write = true,
                no_fill = true,
                no_ledger_write = true,
                required_universe_sha256 = package.Snapshot.RequiredUniverseSha256,
                normalized_line_set_sha256 =
                    package.Snapshot.NormalizedLineSetSha256,
                position_snapshot_id = package.Snapshot.PositionSnapshotId,
                files
            }, json));
        return root;
    }

    private static string Temp()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "qq-arch7b-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Guid Id(int value) =>
        new(value, 0, 0, new byte[8]);

    private static string Sha(char value, int length = 64) =>
        new(value, length);
}
