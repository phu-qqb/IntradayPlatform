using System.Globalization;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arch7bGlobalFlatArguments.Parse(args);
var timing = new Arch7bFreshPositionImportTimingCollector();
var expectations = new Arch7bCoreEvidenceExpectations(
    arguments.CoreRepositoryCommit,
    arguments.ExpectedEvidenceSha256,
    arguments.ExpectedContractFileSha256,
    arguments.ExpectedFinalIndexSha256);
await using var runtime = arguments.BuildRuntime();
var target = runtime.Target;
Require(target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
    "ARCH7B_PMS_TARGET_FINGERPRINT_MISMATCH");
var openTask = runtime.OpenAsync();
var coreTask = Task.Run(() =>
    timing.Measure("CORE_PACKAGE_VALIDATION", () =>
        Arch7bCoreBracketEvidencePackageReader.Read(
            arguments.EvidenceRoot, expectations)));
await Task.WhenAll(openTask, coreTask);
var core = await coreTask;
var openEvidence = await openTask;
var contextFactory = new Arch7bPostgreSqlPinnedDbContextFactory(runtime);
var universe = await timing.MeasureAsync("RDS_UNIVERSE_READ", () =>
    new Arch7bRequiredPmsUniverseReader(
        contextFactory, target, runtime).ReadAsync());
var snapshot = timing.Measure("SNAPSHOT_BUILD", () =>
    Arch7bGlobalFlatPositionSnapshotBuilder.Build(core, universe));

if (arguments.FastPath)
{
    var package = timing.Measure("MINIMAL_PACKAGE_WRITE", () =>
        Arch7bFreshPositionImportPackageWriter.Write(
            arguments.OutputDirectory, core, universe, snapshot));
    var fastTiming = timing.Complete(
        "prepare-fresh-position-import-package",
        core.PositionReportP2Utc,
        smokeAExecuted: false,
        smokeBExecuted: false);
    Arch7bFreshPositionImportTimingWriter.Write(
        arguments.TimingOutput!, package.OutputDirectory, fastTiming);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        result = "ARCH7B_FRESH_POSITION_IMPORT_PACKAGE_PREPARED",
        contract_version = Arch7bFreshPositionImportFastPathContract.Version,
        target = target.ObservableIdentity,
        core.CoreRepositoryCommit,
        core.DownloaderVersion,
        bracket_evidence_sha256 = core.EvidenceSha256,
        successful_attempt_number =
            core.RecomputedSemantics?.SuccessfulAttemptNumber,
        position_report_p2_utc = core.PositionReportP2Utc,
        pms_source_ingestion_id = universe.SourceIngestionId,
        universe.RequiredUniverseSha256,
        snapshot.NormalizedLineCount,
        snapshot.DerivedZeroCount,
        snapshot.UnknownCount,
        snapshot.NormalizedLineSetSha256,
        snapshot.AccountSnapshotId,
        snapshot.PositionSnapshotId,
        snapshot.PositionSnapshotAsOfUtc,
        snapshot.PositionAuthorityCode,
        snapshot.WorkingOrderAuthority,
        snapshot.BrokerSendAllowed,
        smoke_qualification_status =
            Arch7bFreshPositionImportFastPathContract
                .SmokeQualificationStatus,
        smoke_executed = false,
        package.OutputDirectory,
        package.ManifestSha256,
        timing_output = arguments.TimingOutput,
        transaction_read_only = universe.TransactionReadOnly,
        pending_model_changes = universe.PendingModelChanges,
        transport_profile_sha256 = runtime.Profile.Sha256,
        pinned_session = SanitizedSession(openEvidence),
        pinned_session_final = SanitizedSession(runtime.Snapshot()),
        zip_executed = false,
        projection_bundle_written = false,
        no_order = true,
        no_fix = true,
        no_database_write = true,
        no_account_api = true,
        no_databento = true
    }, JsonOptions()));
    return;
}

var smokeA = timing.Measure("SMOKE_A", () =>
    Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe));
var smokeB = timing.Measure("SMOKE_B", () =>
    Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe));
var smokeIsDeterministic = timing.Measure("SMOKE_DETERMINISM", () =>
    Arch7bGlobalFlatOutputWriter.SerializeSmoke(smokeA)
        .SequenceEqual(Arch7bGlobalFlatOutputWriter.SerializeSmoke(smokeB)));
Require(smokeIsDeterministic,
    "ARCH7B_OFFLINE_SMOKE_NONDETERMINISTIC");
var bundle = timing.Measure("FULL_BUNDLE_WRITE", () =>
    Arch7bGlobalFlatOutputWriter.Write(
        arguments.OutputDirectory, core, universe, snapshot, smokeA, smokeB));
Arch7bFreshPositionImportPackageBundle? canonicalPackage = null;
if (arguments.ImportPackageOutput is not null)
{
    canonicalPackage = timing.Measure("CANONICAL_IMPORT_PACKAGE_WRITE", () =>
        Arch7bFreshPositionImportPackageWriter.Write(
            arguments.ImportPackageOutput, core, universe, snapshot));
}
var fullTiming = timing.Complete(
    "consume-bracketed-global-flat",
    core.PositionReportP2Utc,
    smokeAExecuted: true,
    smokeBExecuted: true);
if (arguments.TimingOutput is not null)
{
    Arch7bFreshPositionImportTimingWriter.Write(
        arguments.TimingOutput, bundle.OutputDirectory, fullTiming);
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    result = "ARCH7B_BRACKETED_GLOBAL_FLAT_POSITION_SNAPSHOT_CREATED",
    target = target.ObservableIdentity,
    core.CoreRepositoryCommit,
    core.DownloaderVersion,
    downloader_compatibility_contract =
        core.DownloaderCompatibility?.ContractVersion,
    downloader_compatibility_profile = core.DownloaderCompatibility?.Profile,
    bracket_evidence_sha256 = core.EvidenceSha256,
    successful_attempt_number = core.RecomputedSemantics?.SuccessfulAttemptNumber,
    recomputed_execution_reports = core.RecomputedSemantics?.ExecutionReports,
    recomputed_position_reports = core.RecomputedSemantics?.PositionReports,
    pms_source_ingestion_id = universe.SourceIngestionId,
    pms_source_ingestion_completed_at_utc = universe.IngestionCompletedAtUtc,
    model_as_of_range = new
    {
        earliest = universe.EarliestModelAsOfUtc,
        latest = universe.LatestModelAsOfUtc
    },
    target_close_range = new
    {
        earliest = universe.EarliestTargetCloseUtc,
        latest = universe.LatestTargetCloseUtc
    },
    snapshot.TemporalLineageStatus,
    snapshot.ImportEligibility,
    snapshot.ImportFreshnessStatus,
    universe.MappingCardinalities,
    raw_broker_position_count = core.PositionCount,
    required_instrument_count = universe.Instruments.Count,
    universe.RequiredUniverseSha256,
    snapshot.NormalizedLineCount,
    snapshot.DerivedZeroCount,
    snapshot.UnknownCount,
    snapshot.NormalizedLineSetSha256,
    snapshot.AccountSnapshotId,
    snapshot.PositionSnapshotId,
    snapshot.PositionSnapshotAsOfUtc,
    snapshot.PositionAuthorityCode,
    snapshot.WorkingOrderAuthority,
    snapshot.BrokerSendAllowed,
    smoke = new
    {
        smokeA.ObservationCount,
        smokeA.TargetPositionCount,
        smokeA.PositionOnlyDriftCount,
        smokeA.StrategyCounts,
        smokeA.ProjectionIntegrityStatus,
        deterministic = smokeIsDeterministic
    },
    bundle.OutputDirectory,
    bundle.ManifestSha256,
    canonical_import_package = canonicalPackage,
    timing_output = arguments.TimingOutput,
    transaction_read_only = universe.TransactionReadOnly,
    pending_model_changes = universe.PendingModelChanges,
    transport_profile_sha256 = runtime.Profile.Sha256,
    pinned_session = SanitizedSession(openEvidence),
    pinned_session_final = SanitizedSession(runtime.Snapshot()),
    no_order = true,
    no_fix = true,
    no_database_write = true,
    no_account_api = true,
    no_databento = true
}, JsonOptions()));

static object SanitizedSession(
    Arch7bPostgreSqlPinnedSessionEvidence evidence) => new
    {
        evidence.ContractVersion,
        evidence.TransportProfileVersion,
        evidence.SessionOpenedAtDiagnosticUtc,
        evidence.ColdOpenElapsedMilliseconds,
        evidence.BackendProcessId,
        evidence.PostgreSqlVersion,
        evidence.SessionTimeZone,
        evidence.TlsActive,
        evidence.TransportProfileSha256,
        evidence.SessionLeaseCount,
        evidence.MaximumConcurrentLeases,
        evidence.MaximumLeaseAcquisitionMilliseconds,
        evidence.TransactionCount,
        evidence.PhysicalOpenCount,
        evidence.PhysicalReconnectCount,
        evidence.CloseCount,
        evidence.ConnectionLossObserved,
        evidence.ConnectionState,
        evidence.EvidenceSha256
    };

static JsonSerializerOptions JsonOptions() => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
};

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}

public sealed class Arch7bGlobalFlatArguments
{
    private readonly IReadOnlyDictionary<string, string> values;

    private Arch7bGlobalFlatArguments(
        IReadOnlyDictionary<string, string> values,
        bool fastPath)
    {
        this.values = values;
        FastPath = fastPath;
    }

    public bool FastPath { get; }
    public string EvidenceRoot => Required("--evidence-root");
    public string OutputDirectory => Required("--output-directory");
    public string? ImportPackageOutput => Optional("--import-package-output");
    public string? TimingOutput => Optional("--timing-output");
    public string CoreRepositoryCommit => RequiredSha("--core-repository-commit", 40);
    public string ExpectedEvidenceSha256 => RequiredSha("--expected-evidence-sha256");
    public string ExpectedContractFileSha256 =>
        RequiredSha("--expected-contract-file-sha256");
    public string ExpectedFinalIndexSha256 =>
        RequiredSha("--expected-final-index-sha256");
    public string ExpectedTargetFingerprint =>
        RequiredSha("--expected-target-fingerprint");

    public static Arch7bGlobalFlatArguments Parse(string[] args)
    {
        var full = args.Contains(
            "consume-bracketed-global-flat", StringComparer.Ordinal);
        var fast = args.Contains(
            "prepare-fresh-position-import-package", StringComparer.Ordinal);
        Require(full ^ fast, "ARCH7B_MODE_REQUIRED");
        Require(args.Contains("--no-order", StringComparer.Ordinal),
            "ARCH7B_NO_ORDER_REQUIRED");
        var flags = new HashSet<string>(StringComparer.Ordinal)
        {
            "consume-bracketed-global-flat",
            "prepare-fresh-position-import-package",
            "--no-order"
        };
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (flags.Contains(args[index])) continue;
            Require(args[index].StartsWith("--", StringComparison.Ordinal) &&
                    index + 1 < args.Length,
                "ARCH7B_ARGUMENTS_MUST_BE_NAME_VALUE_PAIRS");
            values.Add(args[index], args[++index]);
        }
        var parsed = new Arch7bGlobalFlatArguments(values, fast);
        Require(parsed.Required("--expected-environment") ==
                Arch7bBracketedGlobalFlatContract.TargetEnvironment,
            "ARCH7B_TARGET_ENVIRONMENT_NOT_TEST");
        Require(parsed.Required("--expected-database") ==
                Arch7bBracketedGlobalFlatContract.TargetDatabase,
            "ARCH7B_TARGET_DATABASE_MISMATCH");
        Require(parsed.Required("--expected-schema") ==
                PmsShadowStateContract.SchemaName,
            "ARCH7B_TARGET_SCHEMA_MISMATCH");
        Require(parsed.Integer("--expected-postgresql-major") ==
                Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
            "ARCH7B_TARGET_POSTGRESQL_MAJOR_MISMATCH");
        Require(parsed.Required("--target-profile") ==
                Arch7bBracketedGlobalFlatContract.TargetProfile,
            "ARCH7B_TARGET_PROFILE_MISMATCH");
        _ = parsed.EvidenceRoot;
        _ = parsed.OutputDirectory;
        _ = parsed.CoreRepositoryCommit;
        _ = parsed.ExpectedEvidenceSha256;
        _ = parsed.ExpectedContractFileSha256;
        _ = parsed.ExpectedFinalIndexSha256;
        _ = parsed.ExpectedTargetFingerprint;
        if (fast)
        {
            Require(parsed.TimingOutput is not null,
                "ARCH7B_POSITION_FAST_PATH_TIMING_OUTPUT_REQUIRED");
            Require(parsed.ImportPackageOutput is null,
                "ARCH7B_POSITION_FAST_PATH_SECOND_PACKAGE_FORBIDDEN");
        }
        return parsed;
    }

    public Arch7bPostgreSqlPinnedSession BuildRuntime()
    {
        Arch7bPostgreSqlPinnedTransportProfileContract.ValidateCommandLine(
            values.Keys, Required("--host"), Integer("--port"));
        var reference = Required("--connection-secret-reference");
        Require(reference.StartsWith("env:", StringComparison.Ordinal),
            "ARCH7B_SECRET_REFERENCE_MUST_USE_ENV");
        var password = Environment.GetEnvironmentVariable(reference[4..]);
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7B_SECRET_VALUE_UNAVAILABLE");
        return Arch7bPostgreSqlPinnedSessionFactory.Create(
            Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary,
            Required("--role"),
            password!,
            FastPath
                ? "QQ_ARCH7B_FRESH_POSITION_FAST_PATH_READONLY"
                : "QQ_ARCH7B_GLOBAL_FLAT_READONLY",
            Arch7bPostgreSqlAccessMode.ReadOnly,
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
            $"ARCH7B_SHA_INVALID:{name}");
        return value;
    }
    private string? Optional(string name) => values.GetValueOrDefault(name);
    private string Required(string name) =>
        values.GetValueOrDefault(name)
        ?? throw new InvalidDataException($"ARCH7B_ARGUMENT_MISSING:{name}");
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
