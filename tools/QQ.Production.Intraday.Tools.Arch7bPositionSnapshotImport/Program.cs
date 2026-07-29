using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arch7bPositionImportArguments.Parse(args);
await using var runtime = arguments.BuildRuntime();
var target = runtime.Target;
Require(target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
    "ARCH7B_POSITION_IMPORT_TARGET_FINGERPRINT_MISMATCH");
var warmTask = runtime.WarmAsync();
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(runtime.DataSource, npgsql => npgsql.SetPostgresVersion(
        Arch7bBracketedGlobalFlatContract.PostgreSqlMajor, 0))
    .Options;
var store = new Arch7bPositionImportStore(
    options, target, runtime.Authority);

if (arguments.Mode == "qualify-database-clock")
{
    var warmEvidence = await warmTask;
    var qualification = await store.QualifyDatabaseClockAsync();
    var evidence = Arch7bPostgreSqlClockQualificationEvidenceWriter.Write(
        arguments.OutputDirectory, qualification, target);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = "ARCH7B_POSTGRESQL_DATABASE_CLOCK_AUTHORITY_QUALIFIED",
        contract_version = qualification.ContractVersion,
        qualification.PostgreSqlVersion,
        qualification.TransactionReadOnly,
        qualification.SamplesMonotonic,
        sample_count = qualification.Samples.Count,
        samples = qualification.Samples.Select(value => new
        {
            value.DatabaseUtc,
            value.EpochDerivedUtc,
            value.TypedVsEpochDeltaMicroseconds,
            value.PostgreSqlType,
            value.SessionTimeZone,
            value.ClrType,
            value.ClrDateTimeKind,
            value.ClrOffset,
            value.EvidenceSha256
        }),
        evidence.OutputDirectory,
        evidence.ManifestSha256,
        evidence.ZipSha256,
        transport_profile_sha256 = runtime.Profile.Sha256,
        warm_connection = warmEvidence,
        warm_connection_final = runtime.Authority.Snapshot(),
        no_database_write = true,
        no_armed_state = true,
        no_owner_lock = true,
        no_ready_marker = true,
        no_lmax_acquisition = true,
        no_order = true
    }, Arch7bPositionImportArguments.Json));
    return;
}

var repository = new GitArch7bRepositoryStateAuthority().Resolve(
    arguments.RepositoryRoot, arguments.BuildCommit);

if (arguments.Mode == "run-fresh-position-import-fast-path")
{
    var expectations = new Arch7bCoreEvidenceExpectations(
        arguments.CoreRepositoryCommit,
        arguments.ExpectedEvidenceSha256,
        arguments.ExpectedContractFileSha256,
        arguments.ExpectedFinalIndexSha256);
    var coreTask = Task.Run(() =>
        Arch7bCoreBracketEvidencePackageReader.Read(
            arguments.EvidenceRoot, expectations));
    await Task.WhenAll(warmTask, coreTask);
    var core = await coreTask;
    var warmEvidence = await warmTask;
    runtime.Authority.EnterPostP2CriticalPath();
    var timeline = new Arch7bFreshPositionImportAppendOnlyTimeline(
        Path.Combine(arguments.OutputDirectory, "append-only-timeline"));
    var currentStage = "PREARMED_VALIDATION";
    DateTimeOffset? lastDatabaseUtc = null;
    try
    {
        var armed = Arch7bPositionImportArmedStateStore.Read(
            arguments.ArmedStatePath);
        Arch7bPositionImportReadyMarkerStore.ValidateOwner(
            arguments.OwnerLockPath, arguments.OwnerId);
        Arch7bFreshPositionImportOrchestrationGuard.RequirePrearmed(
            armed, target, repository, arguments.OwnerId,
            arguments.FutureAuthorizationId,
            arguments.ExpectedSourceIngestionId,
            core.PositionReportP2Utc);

        currentStage = "RDS_UNIVERSE_READ";
        var universe = await new Arch7bRequiredPmsUniverseReader(
                options, target, runtime.Authority)
            .ReadAsync();
        Require(universe.SourceIngestionId ==
                arguments.ExpectedSourceIngestionId,
            "ARCH7B_POSITION_IMPORT_SOURCE_INGESTION_MISMATCH");
        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            core, universe);

        currentStage = "PACKAGE_READY";
        _ = Arch7bFreshPositionImportPackageWriter.Write(
            arguments.PackageRoot, core, universe, snapshot);
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        timeline.Record(Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
            core.PositionReportP2Utc, lastDatabaseUtc.Value));
        var fastPackage = Arch7bPositionImportPackageReader.Read(
            arguments.PackageRoot);
        var fastFreshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
            fastPackage, lastDatabaseUtc.Value, historicalFixture: false);
        Require(fastFreshness.ApplyEligible, fastFreshness.Status);

        currentStage = "READY";
        _ = await store.PlanAsync(fastPackage, fastFreshness);
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        timeline.Record(Arch7bFreshPositionImportSloPolicy.RequireReady(
            core.PositionReportP2Utc, lastDatabaseUtc.Value));
        var fastMarker = Arch7bPositionImportReadyMarkerStore.Create(
            armed, fastPackage, target, repository, lastDatabaseUtc.Value);
        Arch7bPositionImportReadyMarkerStore.PublishAtomic(
            arguments.ReadyMarkerPath, fastMarker);

        currentStage = "PLAN";
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        fastFreshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
            fastPackage, lastDatabaseUtc.Value, historicalFixture: false);
        Require(fastFreshness.ApplyEligible, fastFreshness.Status);
        var fastPlan = await store.PlanAsync(fastPackage, fastFreshness);
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        timeline.Record(Arch7bFreshPositionImportSloPolicy.RequirePlan(
            core.PositionReportP2Utc, lastDatabaseUtc.Value));

        currentStage = "APPLY_START";
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        timeline.Record(Arch7bFreshPositionImportSloPolicy.RequireApplyStart(
            core.PositionReportP2Utc, lastDatabaseUtc.Value));
        var fastResult = await store.ApplyAsync(
            fastPackage, armed, fastMarker, repository,
            arguments.FutureAuthorizationId, arguments.OwnerId);
        currentStage = "COMMIT_READBACK";
        lastDatabaseUtc = await store.ReadDatabaseTimeAsync();
        timeline.Record(
            Arch7bFreshPositionImportSloPolicy.ObserveCommitReadback(
                core.PositionReportP2Utc, lastDatabaseUtc.Value));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            result = fastResult.Status,
            contract_version =
                Arch7bFreshPositionImportFastPathContract.Version,
            package_manifest_sha256 = fastPackage.ManifestSha256,
            package_ready_plan = fastPlan.Status,
            fastResult.PositionSnapshotRowsToAdd,
            fastResult.PositionSnapshotLineRowsToAdd,
            target = target.ObservableIdentity,
            timeline_directory = Path.Combine(
                arguments.OutputDirectory, "append-only-timeline"),
            smoke_qualification_status =
                Arch7bFreshPositionImportFastPathContract
                    .SmokeQualificationStatus,
            transport_profile_sha256 = runtime.Profile.Sha256,
            warm_connection = warmEvidence,
            warm_connection_final = runtime.Authority.Snapshot(),
            no_order = true,
            no_fix = true,
            no_fill = true,
            no_position_ledger_event = true
        }, Arch7bPositionImportArguments.Json));
        return;
    }
    catch (Exception exception)
    {
        try
        {
            timeline.RecordFailure(
                currentStage,
                core.PositionReportP2Utc,
                lastDatabaseUtc,
                exception.Message);
        }
        catch
        {
            // Preserve the first operational blocker.
        }
        throw;
    }
}

_ = await warmTask;

if (arguments.Mode == "arm-import")
{
    var databaseUtc = await store.ArmAsync(
        arguments.ExpectedSourceIngestionId);
    var armed = Arch7bPositionImportArmedStateStore.Create(
        target, repository, arguments.FutureAuthorizationId,
        arguments.OwnerId, arguments.ExpectedSourceIngestionId,
        databaseUtc);
    Arch7bPositionImportReadyMarkerStore.PublishOwner(
        arguments.OwnerLockPath, arguments.OwnerId);
    Arch7bPositionImportArmedStateStore.PublishAtomic(
        arguments.ArmedStatePath, armed);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = "ARMED",
        armed.ArmedAtDatabaseUtc,
        armed.EvidenceSha256,
        target = target.ObservableIdentity,
        repository_commit = repository.HeadCommit,
        build_commit = repository.BuildCommit,
        no_database_write = true,
        no_lmax_acquisition = true,
        no_order = true
    }, Arch7bPositionImportArguments.Json));
    return;
}

var package = Arch7bPositionImportPackageReader.Read(arguments.PackageRoot);
var observedUtc = arguments.Mode == "plan-import"
    ? arguments.ObservedUtc
    : await store.ReadDatabaseTimeAsync();
var freshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
    package, observedUtc, arguments.HistoricalFixture);

if (arguments.Mode == "plan-import")
{
    var plan = await store.PlanAsync(package, freshness);
    var output = Arch7bPositionImportOutputWriter.Write(
        arguments.OutputDirectory, package, freshness, plan, target,
        repository.HeadCommit);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = freshness.Status,
        idempotency = plan.Status,
        output_directory = output,
        plan.TransactionReadOnly,
        plan.PendingModelChanges,
        plan.PositionSnapshotRowsToAdd,
        plan.PositionSnapshotLineRowsToAdd,
        target = target.ObservableIdentity,
        no_database_write = true,
        no_lmax_acquisition = true,
        no_order = true,
        no_fix = true,
        no_fill = true,
        no_position_ledger_event = true
    }, Arch7bPositionImportArguments.Json));
    return;
}

var armedState = Arch7bPositionImportArmedStateStore.Read(
    arguments.ArmedStatePath);
Arch7bPositionImportReadyMarkerStore.ValidateOwner(
    arguments.OwnerLockPath, arguments.OwnerId);

if (arguments.Mode == "publish-ready")
{
    Require(freshness.ApplyEligible, freshness.Status);
    _ = await store.PlanAsync(package, freshness);
    var markerToPublish = Arch7bPositionImportReadyMarkerStore.Create(
        armedState, package, target, repository, observedUtc);
    Arch7bPositionImportReadyMarkerStore.PublishAtomic(
        arguments.ReadyMarkerPath, markerToPublish);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = "READY",
        markerToPublish.ReadyAtDatabaseUtc,
        markerToPublish.PackageManifestSha256,
        markerToPublish.ArmedEvidenceSha256,
        target = target.ObservableIdentity,
        no_database_write = true,
        no_lmax_acquisition = true,
        no_order = true,
    }, Arch7bPositionImportArguments.Json));
    return;
}

Require(arguments.Apply, "ARCH7B_POSITION_IMPORT_EXPLICIT_APPLY_REQUIRED");
Require(!arguments.HistoricalFixture,
    Arch7bPositionImportContract.HistoricalFixture);
Require(freshness.ApplyEligible, freshness.Status);
var marker = Arch7bPositionImportReadyMarkerStore.Read(arguments.ReadyMarkerPath);
var result = await store.ApplyAsync(
    package, armedState, marker, repository,
    arguments.FutureAuthorizationId, arguments.OwnerId);
Console.WriteLine(JsonSerializer.Serialize(new
{
    result = result.Status,
    result.PositionSnapshotRowsToAdd,
    result.PositionSnapshotLineRowsToAdd,
    target = target.ObservableIdentity,
    future_authorization_id = arguments.FutureAuthorizationId,
    no_order = true,
    no_fix = true,
    no_fill = true,
    no_position_ledger_event = true
}, Arch7bPositionImportArguments.Json));

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}

public sealed class Arch7bPositionImportArguments
{
    public static JsonSerializerOptions Json { get; } =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    private readonly IReadOnlyDictionary<string, string> values;
    private readonly IReadOnlySet<string> flags;

    private Arch7bPositionImportArguments(
        string mode,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string> flags)
    {
        Mode = mode;
        this.values = values;
        this.flags = flags;
    }

    public string Mode { get; }
    public string PackageRoot => Required("--package-root");
    public string OutputDirectory => Required("--output-directory");
    public string RepositoryRoot => Path.GetFullPath(
        Required("--repository-root"));
    public string BuildCommit => RequiredSha("--build-commit", 40);
    public string ExpectedTargetFingerprint =>
        RequiredSha("--expected-target-fingerprint");
    public string EvidenceRoot => Required("--evidence-root");
    public string CoreRepositoryCommit =>
        RequiredSha("--core-repository-commit", 40);
    public string ExpectedEvidenceSha256 =>
        RequiredSha("--expected-evidence-sha256");
    public string ExpectedContractFileSha256 =>
        RequiredSha("--expected-contract-file-sha256");
    public string ExpectedFinalIndexSha256 =>
        RequiredSha("--expected-final-index-sha256");
    public Guid ExpectedSourceIngestionId =>
        Guid.Parse(Required("--expected-source-ingestion-id"));
    public bool HistoricalFixture => flags.Contains("--historical-fixture");
    public bool Apply => flags.Contains("--apply");
    public DateTimeOffset ObservedUtc =>
        DateTimeOffset.Parse(Required("--observed-utc"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    public string ReadyMarkerPath => Required("--ready-marker");
    public string ArmedStatePath => Required("--armed-state");
    public string OwnerLockPath => Required("--owner-lock");
    public string OwnerId => Required("--owner-id");
    public string FutureAuthorizationId => Required("--future-authorization-id");

    public static Arch7bPositionImportArguments Parse(string[] args)
    {
        Require(args.Length > 0 &&
                args[0] is "arm-import" or "publish-ready" or
                    "plan-import" or "apply-import" or "qualify-database-clock" or
                    "run-fresh-position-import-fast-path",
            "ARCH7B_POSITION_IMPORT_MODE_REQUIRED");
        Require(args.Contains("--no-order", StringComparer.Ordinal),
            "ARCH7B_POSITION_IMPORT_NO_ORDER_REQUIRED");
        var knownFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--no-order", "--historical-fixture", "--apply"
        };
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index++)
        {
            if (knownFlags.Contains(args[index]))
            {
                flags.Add(args[index]);
                continue;
            }
            Require(args[index].StartsWith("--", StringComparison.Ordinal) &&
                    index + 1 < args.Length,
                "ARCH7B_POSITION_IMPORT_ARGUMENTS_INVALID");
            values.Add(args[index], args[++index]);
        }

        var parsed = new Arch7bPositionImportArguments(args[0], values, flags);
        Require(parsed.Required("--expected-environment") ==
                Arch7bBracketedGlobalFlatContract.TargetEnvironment,
            "ARCH7B_POSITION_IMPORT_ENVIRONMENT_NOT_TEST");
        Require(parsed.Required("--expected-database") ==
                Arch7bBracketedGlobalFlatContract.TargetDatabase,
            "ARCH7B_POSITION_IMPORT_DATABASE_MISMATCH");
        Require(parsed.Required("--expected-schema") ==
                PmsShadowStateContract.SchemaName,
            "ARCH7B_POSITION_IMPORT_SCHEMA_MISMATCH");
        Require(parsed.Integer("--expected-postgresql-major") ==
                Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
            "ARCH7B_POSITION_IMPORT_POSTGRESQL_MAJOR_MISMATCH");
        Require(parsed.Required("--target-profile") ==
                Arch7bBracketedGlobalFlatContract.TargetProfile,
            "ARCH7B_POSITION_IMPORT_PROFILE_MISMATCH");
        _ = parsed.ExpectedTargetFingerprint;
        if (parsed.Mode == "qualify-database-clock")
        {
            Require(!parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_POSITION_IMPORT_CLOCK_QUALIFICATION_FLAGS_INVALID");
            _ = parsed.OutputDirectory;
            return parsed;
        }
        else
        {
            _ = parsed.RepositoryRoot;
            _ = parsed.BuildCommit;
        }
        if (parsed.Mode == "run-fresh-position-import-fast-path")
        {
            Require(parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_POSITION_FAST_PATH_FLAGS_INVALID");
            _ = parsed.PackageRoot;
            _ = parsed.OutputDirectory;
            _ = parsed.EvidenceRoot;
            _ = parsed.CoreRepositoryCommit;
            _ = parsed.ExpectedEvidenceSha256;
            _ = parsed.ExpectedContractFileSha256;
            _ = parsed.ExpectedFinalIndexSha256;
            _ = parsed.ExpectedSourceIngestionId;
            _ = parsed.ArmedStatePath;
            _ = parsed.ReadyMarkerPath;
            _ = parsed.OwnerLockPath;
            _ = parsed.OwnerId;
            _ = parsed.FutureAuthorizationId;
        }
        else if (parsed.Mode == "plan-import")
        {
            Require(!parsed.Apply,
                "ARCH7B_POSITION_IMPORT_PLAN_APPLY_FLAG_FORBIDDEN");
            _ = parsed.OutputDirectory;
            Require(parsed.ObservedUtc.Offset == TimeSpan.Zero,
                "ARCH7B_POSITION_IMPORT_OBSERVED_UTC_REQUIRED");
        }
        else if (parsed.Mode == "arm-import")
        {
            Require(!parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_POSITION_IMPORT_ARM_FLAGS_INVALID");
            _ = parsed.ExpectedSourceIngestionId;
            _ = parsed.ArmedStatePath;
            _ = parsed.OwnerLockPath;
            _ = parsed.OwnerId;
            _ = parsed.FutureAuthorizationId;
        }
        else
        {
            _ = parsed.PackageRoot;
            _ = parsed.ArmedStatePath;
            _ = parsed.ReadyMarkerPath;
            _ = parsed.OwnerLockPath;
            _ = parsed.OwnerId;
            _ = parsed.FutureAuthorizationId;
            if (parsed.Mode == "publish-ready")
                Require(!parsed.Apply && !parsed.HistoricalFixture,
                    "ARCH7B_POSITION_IMPORT_PUBLISH_FLAGS_INVALID");
            else
            {
                Require(parsed.Apply,
                    "ARCH7B_POSITION_IMPORT_EXPLICIT_APPLY_REQUIRED");
                Require(!parsed.HistoricalFixture,
                    Arch7bPositionImportContract.HistoricalFixture);
            }
        }
        return parsed;
    }

    public Arch7bPostgreSqlRuntime BuildRuntime()
    {
        Arch7bPostgreSqlTransportProfileContract.ValidateCommandLine(
            values.Keys, Required("--host"), Integer("--port"));
        var reference = Required("--connection-secret-reference");
        Require(reference.StartsWith("env:", StringComparison.Ordinal),
            "ARCH7B_POSITION_IMPORT_SECRET_REFERENCE_MUST_USE_ENV");
        var password = Environment.GetEnvironmentVariable(reference[4..]);
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7B_POSITION_IMPORT_SECRET_UNAVAILABLE");
        var applicationName = Mode switch
        {
            "arm-import" => "QQ_ARCH7B_POSITION_IMPORT_ARM_READONLY",
            "qualify-database-clock" =>
                "QQ_ARCH7B_POSTGRESQL_CLOCK_QUALIFICATION_READONLY",
            "publish-ready" => "QQ_ARCH7B_POSITION_IMPORT_READY_READONLY",
            "plan-import" => "QQ_ARCH7B_POSITION_IMPORT_PLAN_READONLY",
            "run-fresh-position-import-fast-path" =>
                "QQ_ARCH7B_FRESH_POSITION_IMPORT_FAST_PATH",
            _ => "QQ_ARCH7B_POSITION_IMPORT_APPLY_APPEND_ONLY"
        };
        var accessMode = Mode is "apply-import" or
            "run-fresh-position-import-fast-path"
            ? Arch7bPostgreSqlAccessMode.ApplyAppendOnly
            : Arch7bPostgreSqlAccessMode.ReadOnly;
        return Arch7bPostgreSqlDataSourceFactory.Create(
            Arch7bPostgreSqlTransportProfile.DirectPrimary,
            Required("--role"),
            password!,
            applicationName,
            accessMode,
            Required("--root-certificate"));
    }

    private int Integer(string name) =>
        int.Parse(Required(name), CultureInfo.InvariantCulture);

    private string RequiredSha(string name, int length = 64)
    {
        var value = Required(name);
        Require(value.Length == length &&
                value.All(character =>
                    char.IsAsciiHexDigit(character) && !char.IsUpper(character)),
            $"ARCH7B_POSITION_IMPORT_SHA_INVALID:{name}");
        return value;
    }

    private string Required(string name) =>
        values.GetValueOrDefault(name)
        ?? throw new InvalidDataException(
            $"ARCH7B_POSITION_IMPORT_ARGUMENT_MISSING:{name}");

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
