using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPositionImportContract
{
    public const string Version = "arch7b_bracketed_global_flat_position_import_v1";
    public const string New = "NEW";
    public const string AlreadyAppliedIdentical = "ALREADY_APPLIED_IDENTICAL";
    public const string Conflict = "CONFLICT";
    public const string Eligible = "FRESH_IMPORT_ELIGIBLE";
    public const string HistoricalFixture = "HISTORICAL_FIXTURE_NOT_IMPORT_ELIGIBLE";
    public const string Stale = "ARCH7B_POSITION_BRACKET_STALE";
    public const string FromFuture = "ARCH7B_POSITION_BRACKET_FROM_FUTURE";
    public const string PredatesPmsSource = "ARCH7B_POSITION_BRACKET_PREDATES_PMS_SOURCE";
    public const string NotPrearmed = "ARCH7B_POSITION_IMPORT_NOT_PREARMED";
    public const string AuthorizationMismatch =
        "ARCH7B_POSITION_IMPORT_AUTHORIZATION_MISMATCH";
    public const string OwnerMismatch =
        "ARCH7B_POSITION_IMPORT_OWNER_MISMATCH";
    public const string RepositoryStateMismatch =
        "ARCH7B_POSITION_IMPORT_REPOSITORY_STATE_MISMATCH";
    public const string ChronologyInvalid =
        "ARCH7B_POSITION_IMPORT_CHRONOLOGY_INVALID";
    public const string DatabaseTimezoneInvalid =
        "ARCH7B_POSITION_IMPORT_DATABASE_TIMEZONE_NOT_UTC";
    public const string IntraTransactionReadbackMismatch =
        "ARCH7B_POSITION_IMPORT_INTRA_TRANSACTION_READBACK_MISMATCH";
    public const string PostCommitReadbackMismatch =
        "ARCH7B_POSITION_IMPORT_POST_COMMIT_READBACK_MISMATCH";
    public const int RequiredLineCount = 99;
    public const int MaximumAgeSeconds =
        PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds;
}

public sealed record Arch7bPositionImportPackage(
    string PackageRoot,
    string ManifestSha256,
    Arch7bRequiredPmsUniverse Universe,
    Arch7bPmsGlobalFlatPositionSnapshot Snapshot);

public sealed record Arch7bPositionImportFreshness(
    string Status,
    bool ApplyEligible,
    int MaximumAgeSeconds,
    double AgeSeconds);

public sealed record Arch7bPositionImportDatabaseState(
    bool SourceIngestionExists,
    bool SourceAccountSnapshotExists,
    int SourceModelRunCount,
    int SourceTargetWeightCount,
    int SourceSecurityMappingCount,
    PmsShadowPositionSnapshotRow? ExistingById,
    PmsShadowPositionSnapshotRow? ExistingByEvidence,
    IReadOnlyList<PmsShadowPositionSnapshotLineRow> ExistingLines,
    int PositionSnapshotCountBefore,
    int PositionSnapshotLineCountBefore,
    bool TransactionReadOnly,
    bool PendingModelChanges);

public sealed record Arch7bPositionImportPlan(
    string ContractVersion,
    string Status,
    string ImportEligibility,
    Guid SourceIngestionId,
    Guid SourceAccountSnapshotId,
    Guid PositionSnapshotId,
    string PositionSnapshotSha256,
    string BracketEvidenceSha256,
    string RequiredUniverseSha256,
    string NormalizedLineSetSha256,
    DateTimeOffset PositionSnapshotAsOfUtc,
    int PositionSnapshotRowsToAdd,
    int PositionSnapshotLineRowsToAdd,
    int PositionSnapshotCountBefore,
    int PositionSnapshotCountAfter,
    int PositionSnapshotLineCountBefore,
    int PositionSnapshotLineCountAfter,
    int ModelRunsToAdd,
    int TargetWeightsToAdd,
    int SecurityMappingsToAdd,
    int AccountSnapshotsToAdd,
    bool SourceIngestionMutationRequired,
    bool TransactionReadOnly,
    bool PendingModelChanges,
    bool NoOrder,
    bool NoFix,
    bool NoFill,
    bool NoPositionLedgerEvent);

public sealed record Arch7bPositionImportReadyMarker(
    string ContractVersion,
    string CoreEvidenceSha256,
    string ConsumerSnapshotEvidenceSha256,
    string PackageManifestSha256,
    string ArmedEvidenceSha256,
    string RequiredUniverseSha256,
    string NormalizedLineSetSha256,
    Guid PositionSnapshotId,
    Guid SourceIngestionId,
    Guid SourceAccountSnapshotId,
    DateTimeOffset PositionSnapshotAsOfUtc,
    string TargetProfile,
    string TargetFingerprint,
    string RepositoryCommit,
    string BuildCommit,
    string FutureAuthorizationId,
    string OwnerId,
    DateTimeOffset ReadyAtDatabaseUtc,
    bool NoOrder);

public static class Arch7bPositionImportPackageReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static Arch7bPositionImportPackage Read(string packageRoot)
    {
        var root = Path.GetFullPath(packageRoot);
        Require(Directory.Exists(root), "ARCH7B_POSITION_IMPORT_PACKAGE_MISSING");
        var manifestPath = SafePath(root, "manifest.json");
        var universePath = SafePath(root, "required-pms-universe.json");
        var snapshotPath = SafePath(root,
            "pms-bracketed-global-flat-position-snapshot.json");
        var linesPath = SafePath(root, "normalized-position-lines.csv");
        Require(File.Exists(manifestPath) && File.Exists(universePath) &&
                File.Exists(snapshotPath) && File.Exists(linesPath),
            "ARCH7B_POSITION_IMPORT_PACKAGE_INCOMPLETE");
        RejectReparsePoint(root);

        using var manifestDocument = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var manifest = manifestDocument.RootElement;
        Require(manifest.GetProperty("contract_version").GetString() ==
                Arch7bBracketedGlobalFlatContract.Version,
            "ARCH7B_POSITION_IMPORT_CONSUMER_CONTRACT_MISMATCH");
        Require(True(manifest, "no_order") && True(manifest, "no_fix") &&
                True(manifest, "no_database_write") && True(manifest, "no_fill") &&
                True(manifest, "no_ledger_write"),
            "ARCH7B_POSITION_IMPORT_PACKAGE_SAFETY_INVALID");
        Arch7bPositionImportPackageIntegrity.ValidateInventory(
            root, manifest.GetProperty("files"));

        var universe = JsonSerializer.Deserialize<Arch7bRequiredPmsUniverse>(
            File.ReadAllBytes(universePath), Json)
            ?? throw new InvalidDataException("ARCH7B_POSITION_IMPORT_UNIVERSE_INVALID");
        var snapshot = JsonSerializer.Deserialize<Arch7bPmsGlobalFlatPositionSnapshot>(
            File.ReadAllBytes(snapshotPath), Json)
            ?? throw new InvalidDataException("ARCH7B_POSITION_IMPORT_SNAPSHOT_INVALID");

        Require(snapshot.ContractVersion == Arch7bBracketedGlobalFlatContract.Version,
            "ARCH7B_POSITION_IMPORT_SNAPSHOT_CONTRACT_MISMATCH");
        Require(snapshot.NoOrder && snapshot.NoFix && snapshot.NoDatabaseWrite &&
                !snapshot.BrokerSendAllowed,
            "ARCH7B_POSITION_IMPORT_SNAPSHOT_SAFETY_INVALID");
        Require(snapshot.RawBrokerPositionCount == 0 &&
                snapshot.RequiredInstrumentCount == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.NormalizedLineCount == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.DerivedZeroCount == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.UnknownCount == 0 &&
                snapshot.Lines.Count == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.Lines.All(value => value.CurrentBaseQuantity == 0m),
            "ARCH7B_POSITION_IMPORT_GLOBAL_FLAT_CARDINALITY_INVALID");
        Require(snapshot.RequiredUniverseSha256 == universe.RequiredUniverseSha256 &&
                snapshot.Lines.All(value =>
                    value.SourceIngestionId == universe.SourceIngestionId &&
                    value.PmsSourceSessionId == universe.SourceSessionId &&
                    value.PositionSnapshotId == snapshot.PositionSnapshotId &&
                    value.RequiredUniverseSha256 == universe.RequiredUniverseSha256 &&
                    value.BracketEvidenceSha256 == snapshot.BracketEvidenceSha256),
            "ARCH7B_POSITION_IMPORT_LINEAGE_MISMATCH");
        Require(universe.Instruments.Count == Arch7bPositionImportContract.RequiredLineCount &&
                universe.TransactionReadOnly && universe.NoDatabaseWrite &&
                !universe.PendingModelChanges,
            "ARCH7B_POSITION_IMPORT_UNIVERSE_NOT_QUALIFIED");
        Require(Arch5bHashing.HashCanonical(snapshot.Lines) ==
                snapshot.NormalizedLineSetSha256,
            "ARCH7B_POSITION_IMPORT_LINE_SET_SHA_MISMATCH");
        Require(manifest.GetProperty("required_universe_sha256").GetString() ==
                snapshot.RequiredUniverseSha256 &&
                manifest.GetProperty("normalized_line_set_sha256").GetString() ==
                snapshot.NormalizedLineSetSha256 &&
                manifest.GetProperty("position_snapshot_id").GetGuid() ==
                snapshot.PositionSnapshotId,
            "ARCH7B_POSITION_IMPORT_MANIFEST_BINDING_MISMATCH");
        Arch7bPositionImportPackageIntegrity.ValidateCsvJsonParity(
            linesPath, snapshot.Lines);

        return new(root, FileSha(manifestPath), universe, snapshot);
    }

    private static void RejectReparsePoint(string root)
    {
        var current = new DirectoryInfo(root);
        while (current is not null)
        {
            Require((current.Attributes & FileAttributes.ReparsePoint) == 0,
                "ARCH7B_POSITION_IMPORT_REPARSE_POINT_REJECTED");
            current = current.Parent;
        }
    }

    private static bool True(JsonElement value, string property) =>
        value.GetProperty(property).ValueKind == JsonValueKind.True;

    private static string SafePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Require(path.StartsWith(root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "ARCH7B_POSITION_IMPORT_PATH_ESCAPE");
        return path;
    }

    internal static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public static class Arch7bPositionImportFreshnessPolicy
{
    public static Arch7bPositionImportFreshness Evaluate(
        Arch7bPositionImportPackage package,
        DateTimeOffset observedUtc,
        bool historicalFixture)
    {
        RequireUtc(observedUtc);
        RequireUtc(package.Snapshot.PositionSnapshotAsOfUtc);
        RequireUtc(package.Universe.IngestionCompletedAtUtc);
        var age = observedUtc - package.Snapshot.PositionSnapshotAsOfUtc;
        if (historicalFixture)
            return new(Arch7bPositionImportContract.HistoricalFixture, false,
                Arch7bPositionImportContract.MaximumAgeSeconds, age.TotalSeconds);
        if (package.Snapshot.PositionSnapshotAsOfUtc <
            package.Universe.IngestionCompletedAtUtc)
            return new(Arch7bPositionImportContract.PredatesPmsSource, false,
                Arch7bPositionImportContract.MaximumAgeSeconds, age.TotalSeconds);
        if (age < TimeSpan.Zero)
            return new(Arch7bPositionImportContract.FromFuture, false,
                Arch7bPositionImportContract.MaximumAgeSeconds, age.TotalSeconds);
        if (age > TimeSpan.FromSeconds(Arch7bPositionImportContract.MaximumAgeSeconds))
            return new(Arch7bPositionImportContract.Stale, false,
                Arch7bPositionImportContract.MaximumAgeSeconds, age.TotalSeconds);
        return new(Arch7bPositionImportContract.Eligible, true,
            Arch7bPositionImportContract.MaximumAgeSeconds, age.TotalSeconds);
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException("ARCH7B_POSITION_IMPORT_TIMESTAMP_NOT_UTC");
    }
}

public static class Arch7bPositionImportPlanner
{
    public static Arch7bPositionImportPlan Build(
        Arch7bPositionImportPackage package,
        Arch7bPositionImportFreshness freshness,
        Arch7bPositionImportDatabaseState database)
    {
        if (!database.SourceIngestionExists)
            throw new InvalidDataException("ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISSING");
        if (!database.SourceAccountSnapshotExists)
            throw new InvalidDataException("ARCH7B_POSITION_IMPORT_SOURCE_ACCOUNT_MISSING");
        if (database.PendingModelChanges)
            throw new InvalidDataException("ARCH7B_POSITION_IMPORT_PENDING_MODEL_CHANGES");

        var snapshot = package.Snapshot;
        var expected = new PmsShadowPositionSnapshotRow(
            snapshot.PositionSnapshotId,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            DateOnly.FromDateTime(snapshot.PositionSnapshotAsOfUtc.UtcDateTime),
            snapshot.PositionSnapshotAsOfUtc,
            snapshot.EvidenceSha256,
            true,
            false,
            true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);
        var status = Arch7bPositionImportContract.New;
        if (database.ExistingById is not null || database.ExistingByEvidence is not null)
        {
            var existing = database.ExistingById ?? database.ExistingByEvidence!;
            var identicalHeader = existing == expected;
            var expectedLines = snapshot.Lines.OrderBy(value => value.InstrumentId)
                .Select(value => new PmsShadowPositionSnapshotLineRow(
                    snapshot.PositionSnapshotId, value.InstrumentId, value.SecurityId,
                    value.Symbol, value.CurrentBaseQuantity)).ToArray();
            var identicalLines = database.ExistingLines.OrderBy(value => value.InstrumentId)
                .SequenceEqual(expectedLines);
            status = identicalHeader && identicalLines
                ? Arch7bPositionImportContract.AlreadyAppliedIdentical
                : Arch7bPositionImportContract.Conflict;
        }

        var add = status == Arch7bPositionImportContract.New ? 1 : 0;
        var lineAdd = status == Arch7bPositionImportContract.New
            ? Arch7bPositionImportContract.RequiredLineCount : 0;
        return new(
            Arch7bPositionImportContract.Version,
            status,
            freshness.Status,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            snapshot.PositionSnapshotId,
            snapshot.EvidenceSha256,
            snapshot.BracketEvidenceSha256,
            snapshot.RequiredUniverseSha256,
            snapshot.NormalizedLineSetSha256,
            snapshot.PositionSnapshotAsOfUtc,
            add,
            lineAdd,
            database.PositionSnapshotCountBefore,
            database.PositionSnapshotCountBefore + add,
            database.PositionSnapshotLineCountBefore,
            database.PositionSnapshotLineCountBefore + lineAdd,
            0, 0, 0, 0, false,
            database.TransactionReadOnly,
            database.PendingModelChanges,
            true, true, true, true);
    }
}

public sealed class Arch7bPositionImportStore(
    DbContextOptions<PmsShadowDbContext> options,
    PmsShadowPostgreSqlTarget target)
{
    public async Task<DateTimeOffset> ArmAsync(
        Guid expectedSourceIngestionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = new PmsShadowDbContext(options);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SET TRANSACTION READ ONLY", cancellationToken);
            await ValidateTargetAsync(context, cancellationToken);
            await ValidatePrivilegesAsync(context, cancellationToken);
            var latest = await context.Ingestions.AsNoTracking()
                .OrderByDescending(value => value.StartedAtUtc)
                .ThenByDescending(value => value.IngestionId)
                .FirstOrDefaultAsync(cancellationToken);
            if (latest is null ||
                latest.IngestionId != expectedSourceIngestionId ||
                latest.Status != PmsShadowIngestionStatuses.Completed ||
                latest.CompletedAtUtc is null)
                throw new InvalidDataException(
                    "ARCH7B_POSITION_IMPORT_LATEST_INGESTION_CHANGED");
            var databaseUtc = await ReadDatabaseUtcAsync(
                context, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return databaseUtc;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    public async Task<DateTimeOffset> ReadDatabaseTimeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = new PmsShadowDbContext(options);
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await ValidateTargetAsync(context, cancellationToken);
            return await ReadDatabaseUtcAsync(context, cancellationToken);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    public async Task<Arch7bPositionImportPlan> PlanAsync(
        Arch7bPositionImportPackage package,
        Arch7bPositionImportFreshness freshness,
        CancellationToken cancellationToken = default)
    {
        await using var context = new PmsShadowDbContext(options);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SET TRANSACTION READ ONLY", cancellationToken);
            var readOnly = string.Equals(await ScalarAsync(context,
                    "SHOW transaction_read_only", cancellationToken),
                "on", StringComparison.OrdinalIgnoreCase);
            if (!readOnly)
                throw new InvalidDataException(
                    "ARCH7B_POSITION_IMPORT_PLAN_TRANSACTION_NOT_READ_ONLY");
            await ValidateTargetAsync(context, cancellationToken);
            var state = await ReadStateAsync(context, package, readOnly,
                cancellationToken);
            var plan = Arch7bPositionImportPlanner.Build(package, freshness, state);
            await transaction.RollbackAsync(cancellationToken);
            return plan;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    public async Task<Arch7bPositionImportPlan> ApplyAsync(
        Arch7bPositionImportPackage package,
        Arch7bPositionImportArmedState armed,
        Arch7bPositionImportReadyMarker marker,
        Arch7bRepositoryState repository,
        string expectedFutureAuthorizationId,
        string expectedOwnerId,
        CancellationToken cancellationToken = default)
    {
        await using var context = new PmsShadowDbContext(options);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            await ValidateTargetAsync(context, cancellationToken);
            var lockIdentity = string.Join(':',
                Arch7bPositionImportContract.Version,
                target.TargetProfileId,
                package.Snapshot.AccountId,
                package.Universe.SourceIngestionId.ToString("D"));
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockIdentity}, 0))",
                cancellationToken);
            var applyAtDatabaseUtc = await ReadDatabaseUtcAsync(
                context, cancellationToken);
            Arch7bPositionImportReadyMarkerStore.Validate(
                marker, armed, package, target, repository,
                expectedFutureAuthorizationId, expectedOwnerId,
                applyAtDatabaseUtc);
            var freshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
                package, applyAtDatabaseUtc, historicalFixture: false);
            if (!freshness.ApplyEligible)
                throw new InvalidDataException(freshness.Status);
            await ValidatePrivilegesAsync(context, cancellationToken);
            var protectedCounts = await ProtectedCountsAsync(
                context, cancellationToken);
            var state = await ReadStateAsync(context, package, false,
                cancellationToken);
            var plan = Arch7bPositionImportPlanner.Build(package, freshness, state);
            if (plan.Status == Arch7bPositionImportContract.Conflict)
                throw new InvalidDataException("ARCH7B_POSITION_IMPORT_COLLISION");
            if (plan.Status == Arch7bPositionImportContract.AlreadyAppliedIdentical)
            {
                await VerifyPersistedAsync(context, package,
                    Arch7bPositionImportContract.IntraTransactionReadbackMismatch,
                    cancellationToken);
                await transaction.RollbackAsync(cancellationToken);
                await VerifyPostCommitAsync(
                    package, protectedCounts, cancellationToken);
                return plan;
            }

            context.PositionSnapshots.Add(new(
                package.Snapshot.PositionSnapshotId,
                package.Universe.SourceIngestionId,
                package.Universe.SourceAccountSnapshotId,
                DateOnly.FromDateTime(
                    package.Snapshot.PositionSnapshotAsOfUtc.UtcDateTime),
                package.Snapshot.PositionSnapshotAsOfUtc,
                package.Snapshot.EvidenceSha256,
                true, false, true,
                Arch7bBracketedGlobalFlatContract.PositionAuthorityCode));
            context.PositionSnapshotLines.AddRange(package.Snapshot.Lines.Select(value =>
                new PmsShadowPositionSnapshotLineRow(
                    package.Snapshot.PositionSnapshotId,
                    value.InstrumentId,
                    value.SecurityId,
                    value.Symbol,
                    value.CurrentBaseQuantity)));
            var written = await context.SaveChangesAsync(cancellationToken);
            if (written != Arch7bPositionImportContract.RequiredLineCount + 1)
                throw new InvalidDataException(
                    "ARCH7B_POSITION_IMPORT_ROW_DELTA_MISMATCH");
            await VerifyPersistedAsync(context, package,
                Arch7bPositionImportContract.IntraTransactionReadbackMismatch,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await VerifyPostCommitAsync(
                package, protectedCounts, cancellationToken);
            return plan;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task<Arch7bPositionImportDatabaseState> ReadStateAsync(
        PmsShadowDbContext context,
        Arch7bPositionImportPackage package,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        var ingestion = await context.Ingestions.AsNoTracking()
            .OrderByDescending(value => value.StartedAtUtc)
            .ThenByDescending(value => value.IngestionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISSING");
        if (ingestion.IngestionId != package.Universe.SourceIngestionId ||
            ingestion.SourceSessionId != package.Universe.SourceSessionId ||
            ingestion.Status != PmsShadowIngestionStatuses.Completed ||
            ingestion.CompletedAtUtc is null)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_LATEST_INGESTION_CHANGED");
        var account = await context.AccountSnapshots.AsNoTracking().SingleAsync(value =>
            value.IngestionId == ingestion.IngestionId,
            cancellationToken);
        var models = await context.ModelRuns.AsNoTracking().Where(value =>
            value.IngestionId == ingestion.IngestionId).ToArrayAsync(cancellationToken);
        var modelIds = models.Select(value => value.ModelRunId).ToArray();
        var qubesIds = models.Select(value => value.QubesInputSnapshotId).ToArray();
        var qubes = await context.QubesInputSnapshots.AsNoTracking().Where(value =>
            qubesIds.Contains(value.SnapshotId)).ToArrayAsync(cancellationToken);
        var weights = await context.TargetWeights.AsNoTracking().Where(value =>
            modelIds.Contains(value.ModelRunId)).ToArrayAsync(cancellationToken);
        var mappings = await context.SecurityMappings.AsNoTracking().Where(value =>
            value.IngestionId == ingestion.IngestionId).ToArrayAsync(cancellationToken);
        var databaseUniverse = Arch7bRequiredPmsUniverseBuilder.Build(
            ingestion, account, models, qubes, weights, mappings,
            target.TargetProfileId, target.TargetFingerprint,
            transactionReadOnly: true,
            pendingModelChanges: context.Database.HasPendingModelChanges());
        Arch7bPositionImportUniverseValidator.RequireExact(
            package.Universe, databaseUniverse, target);
        var existingById = await context.PositionSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.PositionSnapshotId == package.Snapshot.PositionSnapshotId,
                cancellationToken);
        var existingByEvidence = await context.PositionSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.IngestionId == package.Universe.SourceIngestionId &&
                value.SnapshotSha256 == package.Snapshot.EvidenceSha256,
                cancellationToken);
        var existingId = existingById?.PositionSnapshotId ??
                         existingByEvidence?.PositionSnapshotId;
        var lines = existingId is null
            ? []
            : await context.PositionSnapshotLines.AsNoTracking()
                .Where(value => value.PositionSnapshotId == existingId.Value)
                .ToArrayAsync(cancellationToken);
        return new(
            true,
            true,
            models.Length,
            weights.Length,
            mappings.Length,
            existingById,
            existingByEvidence,
            lines,
            await context.PositionSnapshots.AsNoTracking().CountAsync(cancellationToken),
            await context.PositionSnapshotLines.AsNoTracking().CountAsync(cancellationToken),
            readOnly,
            context.Database.HasPendingModelChanges());
    }

    private static async Task VerifyPersistedAsync(
        PmsShadowDbContext context,
        Arch7bPositionImportPackage package,
        string blocker,
        CancellationToken cancellationToken)
    {
        var rows = await context.PositionSnapshots.AsNoTracking().Where(value =>
            value.PositionSnapshotId == package.Snapshot.PositionSnapshotId)
            .ToArrayAsync(cancellationToken);
        var lines = await context.PositionSnapshotLines.AsNoTracking().Where(value =>
            value.PositionSnapshotId == package.Snapshot.PositionSnapshotId)
            .OrderBy(value => value.InstrumentId)
            .ToArrayAsync(cancellationToken);
        var expectedRow = new PmsShadowPositionSnapshotRow(
            package.Snapshot.PositionSnapshotId,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            DateOnly.FromDateTime(
                package.Snapshot.PositionSnapshotAsOfUtc.UtcDateTime),
            package.Snapshot.PositionSnapshotAsOfUtc,
            package.Snapshot.EvidenceSha256,
            true, false, true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);
        var expectedLines = package.Snapshot.Lines
            .OrderBy(value => value.InstrumentId)
            .Select(value => new PmsShadowPositionSnapshotLineRow(
                package.Snapshot.PositionSnapshotId,
                value.InstrumentId, value.SecurityId, value.Symbol,
                value.CurrentBaseQuantity)).ToArray();
        if (rows.Length != 1 || rows[0] != expectedRow ||
            !lines.SequenceEqual(expectedLines) ||
            Arch5bHashing.HashCanonical(package.Snapshot.Lines) !=
            package.Snapshot.NormalizedLineSetSha256)
            throw new InvalidDataException(blocker);
    }

    private async Task VerifyPostCommitAsync(
        Arch7bPositionImportPackage package,
        IReadOnlyDictionary<string, int> protectedCounts,
        CancellationToken cancellationToken)
    {
        await using var context = new PmsShadowDbContext(options);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SET TRANSACTION READ ONLY", cancellationToken);
            await VerifyPersistedAsync(context, package,
                Arch7bPositionImportContract.PostCommitReadbackMismatch,
                cancellationToken);
            var after = await ProtectedCountsAsync(context, cancellationToken);
            if (!protectedCounts.OrderBy(value => value.Key)
                    .SequenceEqual(after.OrderBy(value => value.Key)))
                throw new InvalidDataException(
                    Arch7bPositionImportContract.PostCommitReadbackMismatch);
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<IReadOnlyDictionary<string, int>>
        ProtectedCountsAsync(
            PmsShadowDbContext context,
            CancellationToken cancellationToken) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ingestions"] = await context.Ingestions.CountAsync(cancellationToken),
            ["account_snapshots"] = await context.AccountSnapshots.CountAsync(cancellationToken),
            ["model_runs"] = await context.ModelRuns.CountAsync(cancellationToken),
            ["target_weights"] = await context.TargetWeights.CountAsync(cancellationToken),
            ["security_mappings"] = await context.SecurityMappings.CountAsync(cancellationToken),
            ["target_positions"] = await context.TargetPositions.CountAsync(cancellationToken),
            ["position_only_drifts"] = await context.PositionOnlyDrifts.CountAsync(cancellationToken),
            ["qualification_runs"] = await context.Arch7bQualificationRuns.CountAsync(cancellationToken),
            ["fix_session_events"] = await context.Arch7bFixSessionEvents.CountAsync(cancellationToken),
            ["order_send_ledger"] = await context.Arch7bOrderSendLedger.CountAsync(cancellationToken),
            ["execution_reports"] = await context.Arch7bExecutionReports.CountAsync(cancellationToken),
            ["fills"] = await context.Arch7bFills.CountAsync(cancellationToken),
            ["position_ledger_events"] =
                await context.Arch7bPositionLedgerEvents.CountAsync(cancellationToken),
            ["final_reconciliations"] =
                await context.Arch7bFinalReconciliations.CountAsync(cancellationToken)
        };

    private static async Task ValidatePrivilegesAsync(
        PmsShadowDbContext context,
        CancellationToken cancellationToken)
    {
        var sourceSelect = await ScalarBoolAsync(context, """
            SELECT has_table_privilege(current_user,'pms_shadow.ingestions','SELECT')
               AND has_table_privilege(current_user,'pms_shadow.account_snapshots','SELECT')
               AND has_table_privilege(current_user,'pms_shadow.model_runs','SELECT')
               AND has_table_privilege(current_user,'pms_shadow.qubes_input_snapshots','SELECT')
               AND has_table_privilege(current_user,'pms_shadow.target_weights','SELECT')
               AND has_table_privilege(current_user,'pms_shadow.security_mappings','SELECT')
            """, cancellationToken);
        var state = new Arch7bPositionImportPrivilegeState(
            sourceSelect,
            await HasPrivilege(context, "position_snapshots", "INSERT", cancellationToken),
            await HasPrivilege(context, "position_snapshot_lines", "INSERT", cancellationToken),
            await HasPrivilege(context, "position_snapshots", "UPDATE", cancellationToken),
            await HasPrivilege(context, "position_snapshots", "DELETE", cancellationToken),
            await HasPrivilege(context, "position_snapshot_lines", "UPDATE", cancellationToken),
            await HasPrivilege(context, "position_snapshot_lines", "DELETE", cancellationToken),
            await ScalarBoolAsync(context, """
                SELECT has_table_privilege(current_user,'pms_shadow.arch7b_fills','INSERT')
                    OR has_table_privilege(current_user,'pms_shadow.arch7b_position_ledger_events','INSERT')
                    OR has_table_privilege(current_user,'pms_shadow.arch7b_order_send_ledger','INSERT')
                    OR has_table_privilege(current_user,'pms_shadow.target_positions','INSERT')
                    OR has_table_privilege(current_user,'pms_shadow.position_only_drifts','INSERT')
                """, cancellationToken));
        Arch7bPositionImportPrivilegePolicy.RequireExact(state);
    }

    private static Task<bool> HasPrivilege(
        PmsShadowDbContext context,
        string table,
        string privilege,
        CancellationToken cancellationToken) =>
        ScalarBoolAsync(context,
            $"SELECT has_table_privilege(current_user,'pms_shadow.{table}','{privilege}')",
            cancellationToken);

    private static async Task<bool> ScalarBoolAsync(
        PmsShadowDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        return Convert.ToBoolean(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static async Task<DateTimeOffset> ReadDatabaseUtcAsync(
        PmsShadowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await ScalarAsync(context, "SHOW TIMEZONE", cancellationToken) != "UTC")
            throw new InvalidDataException(
                Arch7bPositionImportContract.DatabaseTimezoneInvalid);
        var value = await ScalarAsync(
            context, "SELECT clock_timestamp()", cancellationToken);
        var parsed = DateTimeOffset.Parse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return parsed.ToUniversalTime();
    }

    private async Task ValidateTargetAsync(
        PmsShadowDbContext context,
        CancellationToken cancellationToken)
    {
        if (target.TargetProfileId != Arch7bBracketedGlobalFlatContract.TargetProfile ||
            target.Database != Arch7bBracketedGlobalFlatContract.TargetDatabase ||
            target.ExpectedEnvironment !=
            Arch7bBracketedGlobalFlatContract.TargetEnvironment ||
            target.ExpectedSchema != PmsShadowStateContract.SchemaName ||
            target.ExpectedPostgresMajor !=
            Arch7bBracketedGlobalFlatContract.PostgreSqlMajor)
            throw new InvalidDataException("ARCH7B_POSITION_IMPORT_TARGET_REJECTED");
        if (await ScalarAsync(context, "SELECT current_database()", cancellationToken) !=
            Arch7bBracketedGlobalFlatContract.TargetDatabase)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_DATABASE_IDENTITY_MISMATCH");
        var major = int.Parse(await ScalarAsync(context,
                "SELECT current_setting('server_version_num')", cancellationToken),
            CultureInfo.InvariantCulture) / 10000;
        if (major != Arch7bBracketedGlobalFlatContract.PostgreSqlMajor)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_POSTGRESQL_MAJOR_MISMATCH");
        if (context.Database.HasPendingModelChanges())
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_PENDING_MODEL_CHANGES");
    }

    private static async Task<string> ScalarAsync(
        PmsShadowDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken),
                   CultureInfo.InvariantCulture)
               ?? throw new InvalidDataException(
                   "ARCH7B_POSITION_IMPORT_DATABASE_VALUE_MISSING");
    }
}

public static class Arch7bPositionImportReadyMarkerStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static Arch7bPositionImportReadyMarker Create(
        Arch7bPositionImportArmedState armed,
        Arch7bPositionImportPackage package,
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository,
        DateTimeOffset readyAtDatabaseUtc)
    {
        Arch7bPositionImportArmedStateStore.Validate(
            armed, target, repository, armed.FutureAuthorizationId, armed.OwnerId);
        return new(
            Arch7bPositionImportContract.Version,
            package.Snapshot.BracketEvidenceSha256,
            package.Snapshot.EvidenceSha256,
            package.ManifestSha256,
            armed.EvidenceSha256,
            package.Snapshot.RequiredUniverseSha256,
            package.Snapshot.NormalizedLineSetSha256,
            package.Snapshot.PositionSnapshotId,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            package.Snapshot.PositionSnapshotAsOfUtc,
            target.TargetProfileId,
            target.TargetFingerprint,
            repository.HeadCommit,
            repository.BuildCommit,
            armed.FutureAuthorizationId,
            armed.OwnerId,
            readyAtDatabaseUtc,
            true);
    }

    public static void PublishAtomic(string path, Arch7bPositionImportReadyMarker marker)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, marker, Json);
                stream.Flush(true);
            }
            File.Move(temporary, fullPath, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static Arch7bPositionImportReadyMarker Read(string path) =>
        JsonSerializer.Deserialize<Arch7bPositionImportReadyMarker>(
            File.ReadAllBytes(Path.GetFullPath(path)), Json)
        ?? throw new InvalidDataException("ARCH7B_POSITION_IMPORT_READY_MARKER_INVALID");

    public static void Validate(
        Arch7bPositionImportReadyMarker marker,
        Arch7bPositionImportArmedState armed,
        Arch7bPositionImportPackage package,
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository,
        string expectedFutureAuthorizationId,
        string expectedOwnerId,
        DateTimeOffset applyAtDatabaseUtc)
    {
        if (marker.FutureAuthorizationId != expectedFutureAuthorizationId ||
            armed.FutureAuthorizationId != marker.FutureAuthorizationId)
            throw new InvalidDataException(
                Arch7bPositionImportContract.AuthorizationMismatch);
        if (marker.OwnerId != expectedOwnerId ||
            armed.OwnerId != marker.OwnerId)
            throw new InvalidDataException(
                Arch7bPositionImportContract.OwnerMismatch);
        Arch7bPositionImportArmedStateStore.Validate(
            armed, target, repository,
            expectedFutureAuthorizationId, expectedOwnerId);
        if (marker.ContractVersion != Arch7bPositionImportContract.Version ||
            marker.CoreEvidenceSha256 != package.Snapshot.BracketEvidenceSha256 ||
            marker.ConsumerSnapshotEvidenceSha256 != package.Snapshot.EvidenceSha256 ||
            marker.PackageManifestSha256 != package.ManifestSha256 ||
            marker.ArmedEvidenceSha256 != armed.EvidenceSha256 ||
            marker.RequiredUniverseSha256 != package.Snapshot.RequiredUniverseSha256 ||
            marker.NormalizedLineSetSha256 != package.Snapshot.NormalizedLineSetSha256 ||
            marker.PositionSnapshotId != package.Snapshot.PositionSnapshotId ||
            marker.SourceIngestionId != package.Universe.SourceIngestionId ||
            marker.SourceAccountSnapshotId != package.Universe.SourceAccountSnapshotId ||
            marker.PositionSnapshotAsOfUtc != package.Snapshot.PositionSnapshotAsOfUtc ||
            marker.TargetProfile != target.TargetProfileId ||
            marker.TargetFingerprint != target.TargetFingerprint ||
            marker.RepositoryCommit != repository.HeadCommit ||
            marker.BuildCommit != repository.BuildCommit ||
            marker.RepositoryCommit != marker.BuildCommit ||
            marker.ReadyAtDatabaseUtc.Offset != TimeSpan.Zero ||
            applyAtDatabaseUtc.Offset != TimeSpan.Zero ||
            !marker.NoOrder)
            throw new InvalidDataException(Arch7bPositionImportContract.NotPrearmed);

        if (!(armed.ArmedAtDatabaseUtc <= package.Snapshot.BracketLowerBoundUtc &&
              package.Snapshot.BracketLowerBoundUtc <=
              package.Snapshot.PositionReportP2Utc &&
              package.Snapshot.PositionReportP2Utc <=
              marker.ReadyAtDatabaseUtc &&
              marker.ReadyAtDatabaseUtc <= applyAtDatabaseUtc))
            throw new InvalidDataException(
                Arch7bPositionImportContract.ChronologyInvalid);

        var oneShot = Arch5bHashing.HashCanonical(new
        {
            marker.FutureAuthorizationId,
            marker.PackageManifestSha256,
            marker.PositionSnapshotId,
            marker.TargetFingerprint
        });
        if (!Arch5bHashing.IsSha256(oneShot))
            throw new InvalidDataException(
                Arch7bPositionImportContract.AuthorizationMismatch);
    }

    public static IDisposable AcquireOwner(string path, string ownerId)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            var stream = new FileStream(fullPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            var bytes = Encoding.UTF8.GetBytes(ownerId);
            stream.Write(bytes);
            stream.Flush(true);
            return new OwnerLease(stream, fullPath);
        }
        catch (IOException)
        {
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_OWNER_ALREADY_ACQUIRED");
        }
    }

    public static void PublishOwner(string path, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new InvalidDataException(
                Arch7bPositionImportContract.OwnerMismatch);
        Arch7bPositionImportAtomicFile.Publish(
            path, new { OwnerId = ownerId }, Json);
    }

    public static void ValidateOwner(string path, string ownerId)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(Path.GetFullPath(path)));
        if (document.RootElement.GetProperty("owner_id").GetString() != ownerId)
            throw new InvalidDataException(
                Arch7bPositionImportContract.OwnerMismatch);
    }

    private sealed class OwnerLease(FileStream stream, string path) : IDisposable
    {
        public void Dispose()
        {
            stream.Dispose();
            File.Delete(path);
        }
    }
}

public static class Arch7bPositionImportOutputWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static string Write(
        string outputDirectory,
        Arch7bPositionImportPackage package,
        Arch7bPositionImportFreshness freshness,
        Arch7bPositionImportPlan plan,
        PmsShadowPostgreSqlTarget target,
        string repositoryCommit)
    {
        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_OUTPUT_ALREADY_EXISTS");
        Directory.CreateDirectory(root);
        WriteJson(root, "append-only-import-contract.json", new
        {
            ContractVersion = Arch7bPositionImportContract.Version,
            AppendOnlyTables = new[] { "position_snapshots", "position_snapshot_lines" },
            ExistingSourceIngestionMutated = false,
            AccountSnapshotDuplicated = false,
            ModelRunsDuplicated = false,
            TargetWeightsDuplicated = false,
            SecurityMappingsDuplicated = false,
            MaximumAgeSeconds = Arch7bPositionImportContract.MaximumAgeSeconds,
            ApplyRequiresFreshBracket = true,
            ApplyRequiresPrearmedStateAndReadyMarker = true,
            PositionSnapshotSelectionContract =
                PmsShadowIntradayEconomicContract.PositionSnapshotSelectionVersion,
            NoOrder = true,
            NoFix = true
        });
        WriteJson(root, "persistence-schema-forensic.json", new
        {
            Schema = PmsShadowStateContract.SchemaName,
            PositionSnapshotPrimaryKey = "position_snapshot_id",
            PositionSnapshotIdempotencyKey =
                "(ingestion_id,snapshot_sha256)",
            PositionLinePrimaryKey = "(position_snapshot_id,instrument_id)",
            SourceIngestionForeignKey = package.Universe.SourceIngestionId,
            SourceAccountSnapshotForeignKey =
                package.Universe.SourceAccountSnapshotId,
            ExistingSchemaSupportsAppendOnlyImport = true,
            MigrationRequired = false
        });
        WriteJson(root, "import-plan.json", plan);
        WriteJson(root, "collision-check.json", new
        {
            plan.Status,
            Collision = plan.Status == Arch7bPositionImportContract.Conflict,
            IdempotentReplay = plan.Status ==
                               Arch7bPositionImportContract.AlreadyAppliedIdentical,
            package.Snapshot.PositionSnapshotId,
            package.Snapshot.EvidenceSha256
        });
        WriteJson(root, "expected-row-deltas.json", new
        {
            PositionSnapshots = plan.PositionSnapshotRowsToAdd,
            PositionSnapshotLines = plan.PositionSnapshotLineRowsToAdd,
            AccountSnapshots = 0,
            Ingestions = 0,
            ModelRuns = 0,
            TargetWeights = 0,
            SecurityMappings = 0,
            Fills = 0,
            PositionLedgerEvents = 0
        });
        WriteJson(root, "ready-marker-schema.json", new
        {
            ContractVersion = Arch7bPositionImportContract.Version,
            RequiredBindings = new[]
            {
                "core_evidence_sha256", "consumer_snapshot_evidence_sha256",
                "package_manifest_sha256", "armed_evidence_sha256",
                "required_universe_sha256", "normalized_line_set_sha256",
                "position_snapshot_id", "source_ingestion_id",
                "source_account_snapshot_id", "position_snapshot_as_of_utc",
                "target_profile", "target_fingerprint", "repository_commit",
                "build_commit", "future_authorization_id", "owner_id",
                "ready_at_database_utc", "no_order"
            },
            ArmedStateFile = "importer.armed.json",
            ArmedTimeAuthority = "POSTGRESQL_CLOCK_TIMESTAMP_UTC",
            AtomicPublish = "CREATE_NEW_TEMP_THEN_MOVE_SAME_VOLUME",
            ConcurrentOwnerPolicy = "CREATE_NEW_OWNER_LOCK_FAIL_CLOSED"
        });
        File.WriteAllText(Path.Combine(root, "dry-run-report.md"), $"""
            # ARCH7B Append-Only Global-Flat Position Import Dry Run

            - Result: `{freshness.Status}`
            - Idempotency: `{plan.Status}`
            - Historical fixture apply eligible: `{freshness.ApplyEligible}`
            - Position snapshot ID: `{plan.PositionSnapshotId:D}`
            - Source ingestion ID: `{plan.SourceIngestionId:D}`
            - Source account snapshot ID: `{plan.SourceAccountSnapshotId:D}`
            - Position snapshot rows proposed: `{plan.PositionSnapshotRowsToAdd}`
            - Position snapshot line rows proposed: `{plan.PositionSnapshotLineRowsToAdd}`
            - Model, weight, mapping and account rows proposed: `0`
            - Transaction read-only: `{plan.TransactionReadOnly}`
            - Target: `{target.ObservableIdentity}`
            - Repository commit: `{repositoryCommit}`

            This dry run performed no database write, LMAX acquisition, FIX logon,
            broker send, order, Fill, PositionLedgerEvent, Account API request, or
            Databento request. The historical July 27 fixture is structurally valid
            but cannot be used by `apply-import`.
            """, new UTF8Encoding(false));

        var files = Directory.EnumerateFiles(root)
            .Order(StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path)!,
                Arch7bPositionImportPackageReader.FileSha,
                StringComparer.Ordinal);
        WriteJson(root, "manifest.json", new
        {
            ContractVersion = Arch7bPositionImportContract.Version,
            Result = freshness.Status,
            Idempotency = plan.Status,
            package.ManifestSha256,
            package.Snapshot.BracketEvidenceSha256,
            ConsumerSnapshotEvidenceSha256 = package.Snapshot.EvidenceSha256,
            package.Snapshot.RequiredUniverseSha256,
            package.Snapshot.NormalizedLineSetSha256,
            package.Snapshot.PositionSnapshotId,
            package.Universe.SourceIngestionId,
            package.Universe.SourceAccountSnapshotId,
            target.TargetProfileId,
            target.TargetFingerprint,
            RepositoryCommit = repositoryCommit,
            Files = files,
            NoDatabaseWrite = true,
            NoLmaxAcquisition = true,
            NoOrder = true,
            NoFix = true,
            NoFill = true,
            NoPositionLedgerEvent = true,
            NoAccountApi = true,
            NoDatabento = true
        });
        return root;
    }

    private static void WriteJson(string root, string name, object value) =>
        File.WriteAllBytes(Path.Combine(root, name),
            JsonSerializer.SerializeToUtf8Bytes(value, Json));
}
