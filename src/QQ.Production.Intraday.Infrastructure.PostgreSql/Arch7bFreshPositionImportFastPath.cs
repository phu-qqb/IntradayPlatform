using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bFreshPositionImportFastPathContract
{
    public const string Version = "arch7b_fresh_position_import_fast_path_v1";
    public const string SmokeQualificationStatus =
        "SMOKE_NOT_EXECUTED_IN_CRITICAL_PATH_PREQUALIFIED_CODE_PATH";
    public const int PackageReadySloSeconds = 60;
    public const int ReadySloSeconds = 90;
    public const int PlanSloSeconds = 120;
    public const int ApplyStartSloSeconds = 150;
    public const int CommitReadbackExpectedSeconds = 180;
    public const string PackageSloExceeded =
        "ARCH7B_POSITION_FAST_PATH_PACKAGE_SLO_EXCEEDED";
    public const string ReadySloExceeded =
        "ARCH7B_POSITION_FAST_PATH_READY_SLO_EXCEEDED";
    public const string PlanSloExceeded =
        "ARCH7B_POSITION_FAST_PATH_PLAN_SLO_EXCEEDED";
    public const string ApplyStartSloExceeded =
        "ARCH7B_POSITION_FAST_PATH_APPLY_START_SLO_EXCEEDED";
}

public sealed record Arch7bFreshPositionImportPackageBundle(
    string OutputDirectory,
    string ManifestSha256,
    IReadOnlyDictionary<string, string> FileSha256);

public static class Arch7bFreshPositionImportPackageWriter
{
    private static readonly string[] PackageFiles =
    [
        "normalized-position-lines.csv",
        "pms-bracketed-global-flat-position-snapshot.json",
        "required-pms-universe.json"
    ];

    public static Arch7bFreshPositionImportPackageBundle Write(
        string outputDirectory,
        Arch7bCoreBracketEvidence core,
        Arch7bRequiredPmsUniverse universe,
        Arch7bPmsGlobalFlatPositionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(universe);
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(snapshot.NormalizedLineCount ==
                Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.DerivedZeroCount ==
                Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.UnknownCount == 0 &&
                snapshot.RawBrokerPositionCount == 0,
            "ARCH7B_POSITION_FAST_PATH_GLOBAL_FLAT_INVALID");

        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new InvalidDataException("ARCH7B_OUTPUT_DIRECTORY_ALREADY_EXISTS");
        Directory.CreateDirectory(Path.GetDirectoryName(root)
            ?? throw new InvalidDataException("ARCH7B_OUTPUT_PARENT_INVALID"));
        Directory.CreateDirectory(root);
        try
        {
            Arch7bGlobalFlatOutputWriter.WriteJson(
                root, "required-pms-universe.json", universe);
            Arch7bGlobalFlatOutputWriter.WriteJson(
                root, "pms-bracketed-global-flat-position-snapshot.json", snapshot);
            Arch7bGlobalFlatOutputWriter.WriteCsv(root, snapshot.Lines);

            var files = PackageFiles.ToDictionary(
                name => name,
                name => new Arch7bFreshPositionImportFileIdentity(
                    FileSha(Path.Combine(root, name)),
                    new FileInfo(Path.Combine(root, name)).Length),
                StringComparer.Ordinal);
            var manifest = new
            {
                ContractVersion = Arch7bFreshPositionImportFastPathContract.Version,
                SourceCoreContractVersion = core.CoreContractVersion,
                CoreRepositoryCommit = core.CoreRepositoryCommit,
                core.DownloaderVersion,
                DownloaderCompatibilityContract =
                    core.DownloaderCompatibility?.ContractVersion,
                DownloaderCompatibilityProfile =
                    core.DownloaderCompatibility?.Profile,
                CoreEvidenceSha256 = core.EvidenceSha256,
                SuccessfulAttemptNumber =
                    core.RecomputedSemantics?.SuccessfulAttemptNumber,
                PositionReportP2Utc = core.PositionReportP2Utc,
                SourceIngestionId = universe.SourceIngestionId,
                universe.RequiredUniverseSha256,
                snapshot.PositionSnapshotId,
                snapshot.NormalizedLineSetSha256,
                snapshot.NormalizedLineCount,
                snapshot.DerivedZeroCount,
                snapshot.UnknownCount,
                snapshot.PositionAuthorityCode,
                snapshot.WorkingOrderAuthority,
                snapshot.BrokerSendAllowed,
                SmokeQualificationStatus =
                    Arch7bFreshPositionImportFastPathContract
                        .SmokeQualificationStatus,
                Files = files,
                NoOrder = true,
                NoFix = true,
                NoDatabaseWrite = true,
                NoFill = true,
                NoLedgerWrite = true,
                NoAccountApi = true,
                NoDatabento = true
            };
            Arch7bGlobalFlatOutputWriter.WriteJson(root, "manifest.json", manifest);
            var identities = Directory.EnumerateFiles(root)
                .Order(StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetFileName(path),
                    FileSha,
                    StringComparer.Ordinal);
            Require(identities.Count == 4 &&
                    identities.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
                    PackageFiles.Append("manifest.json")),
                "ARCH7B_POSITION_FAST_PATH_PACKAGE_INVENTORY_INVALID");
            return new(root, identities["manifest.json"], identities);
        }
        catch
        {
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record Arch7bFreshPositionImportFileIdentity(
        string Sha256,
        long SizeBytes);
}

public sealed record Arch7bFreshPositionImportSloDecision(
    string Stage,
    DateTimeOffset BrokerP2Utc,
    DateTimeOffset DatabaseUtc,
    double ElapsedSeconds,
    int MaximumSeconds,
    string Status);

public static class Arch7bFreshPositionImportSloPolicy
{
    public static Arch7bFreshPositionImportSloDecision RequirePackageReady(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc) =>
        Require(brokerP2Utc, databaseUtc,
            Arch7bFreshPositionImportFastPathContract.PackageReadySloSeconds,
            "PACKAGE_READY",
            Arch7bFreshPositionImportFastPathContract.PackageSloExceeded);

    public static Arch7bFreshPositionImportSloDecision RequireReady(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc) =>
        Require(brokerP2Utc, databaseUtc,
            Arch7bFreshPositionImportFastPathContract.ReadySloSeconds,
            "READY",
            Arch7bFreshPositionImportFastPathContract.ReadySloExceeded);

    public static Arch7bFreshPositionImportSloDecision RequirePlan(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc) =>
        Require(brokerP2Utc, databaseUtc,
            Arch7bFreshPositionImportFastPathContract.PlanSloSeconds,
            "PLAN",
            Arch7bFreshPositionImportFastPathContract.PlanSloExceeded);

    public static Arch7bFreshPositionImportSloDecision RequireApplyStart(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc) =>
        Require(brokerP2Utc, databaseUtc,
            Arch7bFreshPositionImportFastPathContract.ApplyStartSloSeconds,
            "APPLY_START",
            Arch7bFreshPositionImportFastPathContract.ApplyStartSloExceeded);

    public static Arch7bFreshPositionImportSloDecision ObserveCommitReadback(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc)
    {
        RequireUtc(brokerP2Utc);
        RequireUtc(databaseUtc);
        var elapsed = (databaseUtc - brokerP2Utc).TotalSeconds;
        if (elapsed < 0)
            throw new InvalidDataException(
                Arch7bPositionImportContract.FromFuture);
        var maximum = Arch7bFreshPositionImportFastPathContract
            .CommitReadbackExpectedSeconds;
        return new(
            "COMMIT_READBACK",
            brokerP2Utc,
            databaseUtc,
            elapsed,
            maximum,
            elapsed <= maximum ? "PASS" : "EXPECTATION_EXCEEDED");
    }

    private static Arch7bFreshPositionImportSloDecision Require(
        DateTimeOffset brokerP2Utc,
        DateTimeOffset databaseUtc,
        int maximumSeconds,
        string stage,
        string blocker)
    {
        RequireUtc(brokerP2Utc);
        RequireUtc(databaseUtc);
        var elapsed = (databaseUtc - brokerP2Utc).TotalSeconds;
        if (elapsed < 0)
            throw new InvalidDataException(
                Arch7bPositionImportContract.FromFuture);
        if (elapsed > maximumSeconds)
            throw new InvalidDataException(blocker);
        return new(stage, brokerP2Utc, databaseUtc, elapsed, maximumSeconds,
            "PASS");
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_TIMESTAMP_NOT_UTC");
    }
}

public sealed record Arch7bFreshPositionImportPhaseTiming(
    string Phase,
    double StartElapsedMilliseconds,
    double EndElapsedMilliseconds,
    double DurationMilliseconds);

public sealed record Arch7bFreshPositionImportTimingEvidence(
    string ContractVersion,
    string Mode,
    DateTimeOffset BrokerP2Utc,
    DateTimeOffset DiagnosticObservedUtc,
    bool HostClockIsEconomicAuthority,
    IReadOnlyList<Arch7bFreshPositionImportPhaseTiming> Phases,
    double TotalElapsedMilliseconds,
    bool SmokeAExecuted,
    bool SmokeBExecuted,
    bool ZipExecuted,
    bool NoOrder,
    bool NoFix,
    bool NoDatabaseWrite);

public sealed class Arch7bFreshPositionImportTimingCollector
{
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<Arch7bFreshPositionImportPhaseTiming> phases = [];

    public T Measure<T>(string phase, Func<T> action)
    {
        var phaseStarted = Stopwatch.GetTimestamp();
        var result = action();
        Add(phase, phaseStarted, Stopwatch.GetTimestamp());
        return result;
    }

    public async Task<T> MeasureAsync<T>(string phase, Func<Task<T>> action)
    {
        var phaseStarted = Stopwatch.GetTimestamp();
        var result = await action();
        Add(phase, phaseStarted, Stopwatch.GetTimestamp());
        return result;
    }

    public Arch7bFreshPositionImportTimingEvidence Complete(
        string mode,
        DateTimeOffset brokerP2Utc,
        bool smokeAExecuted,
        bool smokeBExecuted)
    {
        var ended = Stopwatch.GetTimestamp();
        return new(
            Arch7bFreshPositionImportFastPathContract.Version,
            mode,
            brokerP2Utc,
            DateTimeOffset.UtcNow,
            false,
            phases.ToArray(),
            Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds,
            smokeAExecuted,
            smokeBExecuted,
            false,
            true,
            true,
            true);
    }

    private void Add(string phase, long phaseStarted, long phaseEnded) =>
        phases.Add(new(
            phase,
            Stopwatch.GetElapsedTime(started, phaseStarted).TotalMilliseconds,
            Stopwatch.GetElapsedTime(started, phaseEnded).TotalMilliseconds,
            Stopwatch.GetElapsedTime(phaseStarted, phaseEnded).TotalMilliseconds));
}

public static class Arch7bFreshPositionImportTimingWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static void Write(
        string path,
        string packageRoot,
        Arch7bFreshPositionImportTimingEvidence evidence)
    {
        var fullPath = Path.GetFullPath(path);
        var fullPackage = Path.GetFullPath(packageRoot);
        if (fullPath.StartsWith(
                fullPackage + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "ARCH7B_POSITION_FAST_PATH_TIMING_INSIDE_PACKAGE");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException(
                "ARCH7B_POSITION_FAST_PATH_TIMING_PARENT_INVALID"));
        using var stream = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough);
        JsonSerializer.Serialize(stream, evidence, Json);
        stream.Flush(flushToDisk: true);
    }
}

public static class Arch7bFreshPositionImportOrchestrationGuard
{
    public static void RequirePrearmed(
        Arch7bPositionImportArmedState armed,
        PmsShadowPostgreSqlTarget target,
        Arch7bRepositoryState repository,
        string ownerId,
        string futureAuthorizationId,
        Guid expectedSourceIngestionId,
        DateTimeOffset brokerP2Utc)
    {
        ArgumentNullException.ThrowIfNull(armed);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(repository);
        if (armed.OwnerId != ownerId)
            throw new InvalidDataException(
                Arch7bPositionImportContract.OwnerMismatch);
        if (armed.FutureAuthorizationId != futureAuthorizationId)
            throw new InvalidDataException(
                Arch7bPositionImportContract.AuthorizationMismatch);
        if (armed.ExpectedSourceIngestionId != expectedSourceIngestionId)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISMATCH");
        if (armed.TargetProfile != target.TargetProfileId ||
            armed.TargetFingerprint != target.TargetFingerprint ||
            armed.RepositoryCommit != repository.HeadCommit ||
            armed.BuildCommit != repository.BuildCommit)
            throw new InvalidDataException(
                Arch7bPositionImportContract.RepositoryStateMismatch);
        if (armed.ArmedAtDatabaseUtc > brokerP2Utc)
            throw new InvalidDataException(
                "ARCH7B_POSITION_FAST_PATH_LATE_ARM_REJECTED");
    }
}
public sealed record Arch7bFreshPositionImportTimelineEvent(
    int Sequence,
    string Stage,
    string Status,
    DateTimeOffset BrokerP2Utc,
    DateTimeOffset? DatabaseUtc,
    double? ElapsedSeconds,
    int? MaximumSeconds,
    string? Blocker,
    DateTimeOffset DiagnosticWrittenAtUtc,
    bool HostClockIsEconomicAuthority,
    bool NoOrder,
    bool NoFix);

public sealed class Arch7bFreshPositionImportAppendOnlyTimeline
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string root;
    private int sequence;

    public Arch7bFreshPositionImportAppendOnlyTimeline(string outputDirectory)
    {
        root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new InvalidDataException(
                "ARCH7B_POSITION_FAST_PATH_TIMELINE_ALREADY_EXISTS");
        Directory.CreateDirectory(root);
    }

    public void Record(Arch7bFreshPositionImportSloDecision decision) =>
        Write(new(
            ++sequence,
            decision.Stage,
            decision.Status,
            decision.BrokerP2Utc,
            decision.DatabaseUtc,
            decision.ElapsedSeconds,
            decision.MaximumSeconds,
            null,
            DateTimeOffset.UtcNow,
            false,
            true,
            true));

    public void RecordFailure(
        string stage,
        DateTimeOffset brokerP2Utc,
        DateTimeOffset? databaseUtc,
        string blocker)
    {
        double? elapsed = databaseUtc is null
            ? null
            : (databaseUtc.Value - brokerP2Utc).TotalSeconds;
        Write(new(
            ++sequence,
            stage,
            "BLOCKED",
            brokerP2Utc,
            databaseUtc,
            elapsed,
            null,
            blocker,
            DateTimeOffset.UtcNow,
            false,
            true,
            true));
    }

    private void Write(Arch7bFreshPositionImportTimelineEvent value)
    {
        var safeStage = string.Concat(value.Stage.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '_'));
        var path = Path.Combine(root,
            $"{value.Sequence:D4}-{safeStage.ToLowerInvariant()}.json");
        using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            4096, FileOptions.WriteThrough);
        JsonSerializer.Serialize(stream, value, Json);
        stream.Flush(flushToDisk: true);
    }
}
