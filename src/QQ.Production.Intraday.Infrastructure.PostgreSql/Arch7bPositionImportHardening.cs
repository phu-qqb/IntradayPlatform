using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPositionImportPackageIntegrity
{
    public const string CsvJsonMismatch =
        "ARCH7B_POSITION_IMPORT_CSV_JSON_MISMATCH";

    public static void ValidateInventory(
        string root,
        JsonElement manifestFiles,
        string manifestFileName = "manifest.json")
    {
        var declared = manifestFiles.EnumerateObject().ToArray();
        var declaredNames = declared.Select(value => value.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualNames = Directory.EnumerateFileSystemEntries(root)
            .Select(Path.GetFileName)
            .Select(value => value ?? throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_PACKAGE_INVENTORY_MISMATCH")).Where(value => !string.Equals(
                value, manifestFileName, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        Require(declaredNames.SetEquals(actualNames),
            "ARCH7B_POSITION_IMPORT_PACKAGE_INVENTORY_MISMATCH");

        foreach (var item in declared)
        {
            Require(!Path.IsPathRooted(item.Name) &&
                    item.Name == Path.GetFileName(item.Name) &&
                    !item.Name.Contains("..", StringComparison.Ordinal),
                "ARCH7B_POSITION_IMPORT_PATH_ESCAPE");
            var path = Path.Combine(root, item.Name);
            var attributes = File.GetAttributes(path);
            Require((attributes & FileAttributes.ReparsePoint) == 0,
                "ARCH7B_POSITION_IMPORT_REPARSE_POINT_REJECTED");
            Require((attributes & FileAttributes.Directory) == 0,
                "ARCH7B_POSITION_IMPORT_PACKAGE_INVENTORY_MISMATCH");

            var (expectedSha, expectedSize) = ManifestIdentity(item.Value);
            var bytes = File.ReadAllBytes(path);
            Require(bytes.LongLength > 0 &&
                    Convert.ToHexStringLower(SHA256.HashData(bytes)) == expectedSha &&
                    (expectedSize is null || expectedSize == bytes.LongLength),
                "ARCH7B_POSITION_IMPORT_MANIFEST_FILE_IDENTITY_MISMATCH");
        }
    }

    public static void ValidateCsvJsonParity(
        string csvPath,
        IReadOnlyList<Arch7bNormalizedPositionLine> jsonLines)
    {
        var rows = ReadCsv(csvPath);
        Require(rows.Count == jsonLines.Count &&
                rows.OrderBy(value => value.PositionSnapshotLineId)
                    .SequenceEqual(jsonLines.OrderBy(
                        value => value.PositionSnapshotLineId)),
            CsvJsonMismatch);
    }

    public static IReadOnlyList<Arch7bNormalizedPositionLine> ReadCsv(
        string csvPath)
    {
        var rows = File.ReadAllLines(csvPath, Encoding.UTF8);
        Require(rows.Length > 1, CsvJsonMismatch);
        var expectedHeader = new[]
        {
            "position_snapshot_line_id", "position_snapshot_id",
            "instrument_id", "security_id", "symbol", "lmax_instrument_id",
            "mapping_sha256", "source_ingestion_id", "pms_source_session_id",
            "current_base_quantity", "provenance_kind",
            "position_authority_code", "account_id", "broker_position_count",
            "bracket_evidence_sha256", "required_universe_sha256",
            "core_repository_commit", "position_snapshot_as_of_utc",
            "evidence_sha256"
        };
        Require(ParseCsvRow(rows[0]).SequenceEqual(expectedHeader),
            CsvJsonMismatch);
        return rows.Skip(1).Select(ParsePositionLine).ToArray();
    }

    private static Arch7bNormalizedPositionLine ParsePositionLine(string row)
    {
        var values = ParseCsvRow(row);
        Require(values.Count == 19, CsvJsonMismatch);
        try
        {
            return new(
                Guid.Parse(values[0]), Guid.Parse(values[1]),
                Guid.Parse(values[2]), values[3], values[4], values[5],
                values[6], Guid.Parse(values[7]), values[8],
                decimal.Parse(values[9], NumberStyles.Number,
                    CultureInfo.InvariantCulture),
                values[10], values[11], values[12],
                int.Parse(values[13], CultureInfo.InvariantCulture),
                values[14], values[15], values[16],
                DateTimeOffset.Parse(values[17], CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                values[18]);
        }
        catch (Exception exception) when (
            exception is FormatException or OverflowException)
        {
            throw new InvalidDataException(CsvJsonMismatch, exception);
        }
    }

    private static IReadOnlyList<string> ParseCsvRow(string row)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < row.Length; index++)
        {
            var character = row[index];
            if (character == '"')
            {
                if (quoted && index + 1 < row.Length && row[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        Require(!quoted, CsvJsonMismatch);
        values.Add(current.ToString());
        return values;
    }

    private static (string Sha256, long? SizeBytes) ManifestIdentity(
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return (RequiredSha(value.GetString()), null);
        Require(value.ValueKind == JsonValueKind.Object,
            "ARCH7B_POSITION_IMPORT_MANIFEST_FILE_IDENTITY_INVALID");
        return (
            RequiredSha(value.GetProperty("sha256").GetString()),
            value.GetProperty("size_bytes").GetInt64());
    }

    private static string RequiredSha(string? value)
    {
        Require(value is not null && Arch5bHashing.IsSha256(value),
            "ARCH7B_POSITION_IMPORT_MANIFEST_FILE_IDENTITY_INVALID");
        return value!;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bPositionImportArmedState(
    string ContractVersion,
    string TargetProfile,
    string TargetFingerprint,
    string Database,
    string Schema,
    int ExpectedPostgreSqlMajor,
    string RepositoryCommit,
    string BuildCommit,
    string FutureAuthorizationId,
    string OwnerId,
    string ExpectedAccountId,
    string ExpectedEnvironment,
    Guid ExpectedSourceIngestionId,
    DateTimeOffset ArmedAtDatabaseUtc,
    bool NoOrder,
    string EvidenceSha256);

public static class Arch7bPositionImportArmedStateStore
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    public static Arch7bPositionImportArmedState Create(
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository,
        string futureAuthorizationId,
        string ownerId,
        Guid expectedSourceIngestionId,
        DateTimeOffset armedAtDatabaseUtc)
    {
        var core = new
        {
            ContractVersion = Arch7bPositionImportContract.Version,
            target.TargetProfileId,
            target.TargetFingerprint,
            target.Database,
            Schema = target.ExpectedSchema,
            target.ExpectedPostgresMajor,
            RepositoryCommit = repository.HeadCommit,
            repository.BuildCommit,
            FutureAuthorizationId = futureAuthorizationId,
            OwnerId = ownerId,
            ExpectedAccountId = Arch7bBracketedGlobalFlatContract.AccountId,
            ExpectedEnvironment =
                Arch7bBracketedGlobalFlatContract.TargetEnvironment,
            ExpectedSourceIngestionId = expectedSourceIngestionId,
            ArmedAtDatabaseUtc = armedAtDatabaseUtc,
            NoOrder = true
        };
        return new(
            Arch7bPositionImportContract.Version,
            target.TargetProfileId, target.TargetFingerprint, target.Database,
            target.ExpectedSchema, target.ExpectedPostgresMajor,
            repository.HeadCommit, repository.BuildCommit,
            futureAuthorizationId, ownerId,
            Arch7bBracketedGlobalFlatContract.AccountId,
            Arch7bBracketedGlobalFlatContract.TargetEnvironment,
            expectedSourceIngestionId, armedAtDatabaseUtc, true,
            Arch5bHashing.HashCanonical(core));
    }

    public static void PublishAtomic(
        string path, Arch7bPositionImportArmedState state) =>
        Arch7bPositionImportAtomicFile.Publish(path, state, Json);

    public static Arch7bPositionImportArmedState Read(string path) =>
        JsonSerializer.Deserialize<Arch7bPositionImportArmedState>(
            File.ReadAllBytes(Path.GetFullPath(path)), Json)
        ?? throw new InvalidDataException(
            "ARCH7B_POSITION_IMPORT_ARMED_STATE_INVALID");

    public static void Validate(
        Arch7bPositionImportArmedState state,
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository,
        string expectedFutureAuthorizationId,
        string expectedOwnerId)
    {
        var recreated = Create(target, repository,
            expectedFutureAuthorizationId, expectedOwnerId,
            state.ExpectedSourceIngestionId, state.ArmedAtDatabaseUtc);
        if (state != recreated ||
            state.ArmedAtDatabaseUtc.Offset != TimeSpan.Zero ||
            !state.NoOrder)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_ARMED_STATE_MISMATCH");
    }
}

public static class Arch7bPositionImportAtomicFile
{
    public static void Publish<T>(
        string path, T value, JsonSerializerOptions json)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None, 4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, json);
                stream.Flush(true);
            }
            File.Move(temporary, fullPath, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed record Arch7bRepositoryState(
    string ContractVersion,
    string RepositoryRoot,
    string HeadCommit,
    string BuildCommit,
    bool IndexClean,
    bool WorktreeClean);

public interface IArch7bRepositoryStateAuthority
{
    Arch7bRepositoryState Resolve(
        string repositoryRoot, string expectedBuildCommit);
}

public sealed class GitArch7bRepositoryStateAuthority :
    IArch7bRepositoryStateAuthority
{
    public const string ContractVersion =
        "institutional_repository_state_authority_v1";

    public Arch7bRepositoryState Resolve(
        string repositoryRoot, string expectedBuildCommit)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var actualRoot = Run(root, "rev-parse", "--show-toplevel");
        var head = Run(root, "rev-parse", "HEAD");
        var status = Run(root,
            ["status", "--porcelain=v1", "--untracked-files=all"],
            allowEmpty: true);
        var clean = string.IsNullOrWhiteSpace(status);
        if (!string.Equals(
                Path.GetFullPath(actualRoot), root,
                StringComparison.OrdinalIgnoreCase) ||
            !GitCommitIdentityContract.IsValid(head, "sha1") ||
            head != expectedBuildCommit || !clean)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_REPOSITORY_STATE_MISMATCH");
        return new(ContractVersion, root, head, expectedBuildCommit,
            IndexClean: true, WorktreeClean: true);
    }

    private static string Run(
        string root, params string[] arguments) =>
        Run(root, arguments, allowEmpty: false);

    private static string Run(
        string root, string[] arguments, bool allowEmpty)
    {
        using var process = new Process
        {
            StartInfo = new("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || (!allowEmpty && output.Length == 0))
            throw new InvalidDataException(
                Arch7bPositionImportContract.RepositoryStateMismatch +
                " " + error.Trim());
        return output;
    }
}

public static class Arch7bPositionImportUniverseValidator
{
    public static void RequireExact(
        Arch7bRequiredPmsUniverse package,
        Arch7bRequiredPmsUniverse database,
        PmsShadowPostgreSqlTarget target)
    {
        if (package.SourceIngestionId != database.SourceIngestionId ||
            package.SourceSessionId != database.SourceSessionId)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_LATEST_INGESTION_CHANGED");
        if (package.TargetProfile != target.TargetProfileId ||
            package.TargetFingerprint != target.TargetFingerprint ||
            database.TargetProfile != target.TargetProfileId ||
            database.TargetFingerprint != target.TargetFingerprint)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_TARGET_FINGERPRINT_CHANGED");
        if (Arch5bHashing.HashCanonical(package.Models) !=
                Arch5bHashing.HashCanonical(database.Models) ||
            Arch5bHashing.HashCanonical(
                package.StrategyCounts.OrderBy(value => value.Key)) !=
            Arch5bHashing.HashCanonical(
                database.StrategyCounts.OrderBy(value => value.Key)))
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_MODEL_LINEAGE_CHANGED");
        if (Arch5bHashing.HashCanonical(package.QubesInputs) !=
            Arch5bHashing.HashCanonical(database.QubesInputs))
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_QUBES_LINEAGE_CHANGED");
        if (Arch5bHashing.HashCanonical(package.Mappings) !=
                Arch5bHashing.HashCanonical(database.Mappings) ||
            Arch5bHashing.HashCanonical(package.Instruments) !=
                Arch5bHashing.HashCanonical(database.Instruments) ||
            package.MappingCardinalities != database.MappingCardinalities)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_MAPPING_LINEAGE_CHANGED");
        if (package.RequiredUniverseSha256 != database.RequiredUniverseSha256)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_UNIVERSE_SHA_MISMATCH");
        if (package.SourceAccountSnapshotId !=
                database.SourceAccountSnapshotId ||
            package.NavUsd != database.NavUsd ||
            package.IngestionCompletedAtUtc !=
                database.IngestionCompletedAtUtc)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_SOURCE_ACCOUNT_CHANGED");
    }
}

public sealed record Arch7bPositionImportPrivilegeState(
    bool SourceSelect,
    bool PositionSnapshotInsert,
    bool PositionSnapshotLineInsert,
    bool PositionSnapshotUpdate,
    bool PositionSnapshotDelete,
    bool PositionSnapshotLineUpdate,
    bool PositionSnapshotLineDelete,
    bool ForbiddenInsert);

public static class Arch7bPositionImportPrivilegePolicy
{
    public static void RequireExact(
        Arch7bPositionImportPrivilegeState state)
    {
        if (!state.SourceSelect ||
            !state.PositionSnapshotInsert ||
            !state.PositionSnapshotLineInsert)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_REQUIRED_PRIVILEGE_MISSING");
        if (state.PositionSnapshotUpdate ||
            state.PositionSnapshotLineUpdate)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_UPDATE_PRIVILEGE_FORBIDDEN");
        if (state.PositionSnapshotDelete ||
            state.PositionSnapshotLineDelete)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_DELETE_PRIVILEGE_FORBIDDEN");
        if (state.ForbiddenInsert)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_LIFECYCLE_PRIVILEGE_FORBIDDEN");
    }
}
