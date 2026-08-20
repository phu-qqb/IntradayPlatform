using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arch7bPositionImportArguments.Parse(args);
if (arguments.Mode == "qualify-core-to-position-consumer-offline-bridge")
{
    var result = Arch7bOfflineCoreConsumerBridgeRunner.Run(new(
        arguments.EvidenceRoot,
        arguments.OutputDirectory,
        arguments.CoreRepositoryCommit,
        arguments.CoreTree,
        arguments.IntradayRepositoryCommit,
        arguments.IntradayTree,
        arguments.ExpectedEvidenceSha256,
        arguments.ExpectedContractFileSha256,
        arguments.ExpectedFinalIndexSha256,
        arguments.ExpectedAcquisitionManifestSha256,
        arguments.ExpectedBracketContractVersion,
        arguments.ExpectedDownloaderVersion,
        arguments.ExpectedAccount,
        arguments.ExpectedSourceSessionId,
        arguments.ExpectedExecutionSemanticSha256,
        arguments.ExpectedPositionSemanticSha256,
        arguments.ExpectedSourceIngestionId,
        arguments.ExpectedRequiredUniverseSha256,
        arguments.ExpectedPositionReportP2Utc,
        arguments.ExpectedNormalizedCount,
        arguments.ExpectedDerivedZeroCount,
        arguments.ExpectedUnknownCount,
        Environment.ProcessPath ?? throw new InvalidDataException(
            "ARCH7B_BRIDGE_CONSUMER_EXECUTABLE_MISSING"),
        arguments.ExpectedConsumerExecutableSha256,
        arguments.SyntheticRunId,
        arguments.SyntheticOwnerId,
        arguments.SyntheticFutureAuthorizationId));
    Console.WriteLine(JsonSerializer.Serialize(
        result, Arch7bPositionImportArguments.Json));
    return;
}

if (arguments.Mode == "qualify-repository-authority")
{
    var git = arguments.BuildGitAuthority();
    var repository = new GitArch7bRepositoryStateAuthority().Resolve(
        arguments.RepositoryRoot, arguments.BuildCommit, git);
    Require(repository.HeadCommit == arguments.ExpectedRepositoryHead,
        "ARCH7B_POSITION_IMPORT_REPOSITORY_STATE_MISMATCH");
    var evidence = Arch7bRepositoryAuthorityEvidenceWriter.Write(
        arguments.OutputDirectory, git, repository);
    Console.WriteLine(JsonSerializer.Serialize(
        evidence, Arch7bPositionImportArguments.Json));
    return;
}

if (arguments.Mode == "qualify-runtime-selection")
{
    var result = Arch7bRuntimeSelectionQualificationRunner.Run(new(
        arguments.PackageRoot,
        arguments.OutputDirectory,
        arguments.ExpectedAccount,
        arguments.ExpectedTargetFingerprint,
        arguments.ExpectedSourceSessionId,
        arguments.ExpectedSourceIngestionId,
        arguments.ExpectedPositionSnapshotId));
    Console.WriteLine(JsonSerializer.Serialize(
        result, Arch7bPositionImportArguments.Json));
    return;
}

var gitAuthority = arguments.RequiresRepository
    ? arguments.BuildGitAuthority()
    : null;
var prevalidatedRepository = arguments.RequiresRepository
    ? new GitArch7bRepositoryStateAuthority().Resolve(
        arguments.RepositoryRoot, arguments.BuildCommit, gitAuthority!)
    : null;
var runtime = arguments.BuildRuntime();
var target = runtime.Target;
Require(target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
    "ARCH7B_POSITION_IMPORT_TARGET_FINGERPRINT_MISMATCH");
var contextFactory = new Arch7bPostgreSqlPinnedDbContextFactory(runtime);
var store = new Arch7bPositionImportStore(
    contextFactory, target, runtime);
var supervisor = new Arch7bPostgreSqlPinnedOpenSupervisor(
    runtime, arguments.Mode, arguments.LifecycleEvidenceDirectory);
var openTask = supervisor.StartOpen();
ExceptionDispatchInfo? primaryFailure = null;
try
{

if (arguments.Mode == "qualify-pinned-postgresql-session")
{
    var openEvidence = await supervisor.WaitForOpenAsync();
    for (var index = 0; index < 10; index++)
    {
        await using var lease = await runtime.AcquireAsync();
    }
    var qualification = await store.QualifyDatabaseClockAsync();
    var universe = await new Arch7bRequiredPmsUniverseReader(
            contextFactory, target, runtime)
        .ReadAsync();
    var qualificationPackage = Arch7bPositionImportPackageReader.Read(
        arguments.PackageRoot);
    var historicalFreshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
        qualificationPackage, qualification.Samples[^1].DatabaseUtc,
        historicalFixture: true);
    Require(historicalFreshness.Status ==
            Arch7bPositionImportContract.HistoricalFixture &&
            !historicalFreshness.ApplyEligible,
        "ARCH7B_PINNED_SESSION_HISTORICAL_PLAN_ELIGIBILITY_INVALID");
    var plan = await store.PlanAsync(
        qualificationPackage, historicalFreshness);
    Require(plan.Status is Arch7bPositionImportContract.New or
            Arch7bPositionImportContract.AlreadyAppliedIdentical,
        "ARCH7B_PINNED_SESSION_HISTORICAL_PLAN_INVALID");
    _ = await store.ReadDatabaseTimeAsync();
    var beforeClose = runtime.Snapshot();
    _ = await supervisor.CompleteAsync();
    var finalSession = runtime.Snapshot();
    var output = new
    {
        result = "ARCH7B_PINNED_POSTGRESQL_SESSION_QUALIFIED",
        contract_version =
            Arch7bPostgreSqlPinnedSessionAuthority.Version,
        transport_profile_version = runtime.Profile.ContractVersion,
        selected_profile = runtime.Profile.Profile,
        pooling = runtime.Profile.Pooling,
        open_evidence = new
        {
            openEvidence.ColdOpenElapsedMilliseconds,
            openEvidence.BackendProcessId,
            openEvidence.PostgreSqlVersion,
            openEvidence.SessionTimeZone,
            openEvidence.TlsActive,
            openEvidence.PhysicalOpenCount,
            openEvidence.PhysicalReconnectCount
        },
        session_before_close = SanitizedSession(beforeClose),
        session_final = SanitizedSession(finalSession),
        clock = new
        {
            qualification.PostgreSqlVersion,
            qualification.TransactionReadOnly,
            qualification.SamplesMonotonic,
            samples = qualification.Samples
        },
        universe = new
        {
            instrument_count = universe.Instruments.Count,
            model_count = universe.Models.Count,
            qubes_count = universe.QubesInputs.Count,
            mapping_count = universe.Mappings.Count,
            universe.TransactionReadOnly,
            universe.PendingModelChanges,
            universe.RequiredUniverseSha256
        },
        historical_plan_result =
            Arch7bPositionImportContract.HistoricalFixture,
        historical_plan_idempotency = plan.Status,
        same_pid_proven = beforeClose.BackendProcessId ==
            openEvidence.BackendProcessId,
        no_database_write = true,
        no_lmax_acquisition = true,
        no_order = true
    };
    Directory.CreateDirectory(arguments.OutputDirectory);
    var json = JsonSerializer.Serialize(
        output, Arch7bPositionImportArguments.Json);
    File.WriteAllText(Path.Combine(arguments.OutputDirectory,
        "pinned-postgresql-session-qualification.json"), json);
    Console.WriteLine(json);
    return;
}

if (arguments.Mode == "qualify-database-clock")
{
    var openEvidence = await supervisor.WaitForOpenAsync();
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
        pinned_session = SanitizedSession(openEvidence),
        pinned_session_final = SanitizedSession(runtime.Snapshot()),
        no_database_write = true,
        no_armed_state = true,
        no_owner_lock = true,
        no_ready_marker = true,
        no_lmax_acquisition = true,
        no_order = true
    }, Arch7bPositionImportArguments.Json));
    return;
}

var repository = prevalidatedRepository ?? throw new InvalidDataException(
    "ARCH7B_POSITION_IMPORT_REPOSITORY_PREVALIDATION_MISSING");

if (arguments.Mode == "resolve-arm-preconditions")
{
    var openEvidence = await supervisor.WaitForOpenAsync();
    var universe = await new Arch7bRequiredPmsUniverseReader(
            contextFactory, target, runtime)
        .ReadAsync();
    var resolution = Arch7bPreArmBindingResolver.Resolve(
        arguments.RunId, arguments.OwnerId, arguments.FutureAuthorizationId,
        universe, target, repository);
    var evidencePath = Arch7bPreArmBindingResolutionStore.Publish(
        arguments.OutputDirectory, resolution);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        resolution.ContractVersion,
        resolution.Result,
        resolution.RunId,
        resolution.OwnerIdSha256,
        resolution.FutureAuthorizationIdSha256,
        resolution.SourceIngestionId,
        resolution.SourceIngestionCompletedAtUtc,
        resolution.SourceSessionId,
        resolution.RequiredUniverseSha256,
        resolution.SourceSelectionAuthority,
        resolution.TargetProfile,
        resolution.TargetFingerprint,
        resolution.RepositoryAuthorityContract,
        resolution.RepositoryCommit,
        resolution.BuildCommit,
        resolution.TransactionReadOnly,
        resolution.PendingModelChanges,
        resolution.NoDatabaseWrite,
        resolution.NoArmedState,
        resolution.NoOwnerLock,
        resolution.NoReadyMarker,
        resolution.NoLmaxAcquisition,
        resolution.NoFix,
        resolution.NoBroker,
        resolution.NoOrder,
        evidence_path = evidencePath,
        pinned_session = SanitizedSession(openEvidence)
    }, Arch7bPositionImportArguments.Json));
    return;
}

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
    var parallel = await supervisor.WaitForOpenAndPeerAsync(coreTask);
    var core = parallel.PeerResult;
    var openEvidence = parallel.OpenEvidence;
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
                contextFactory, target, runtime)
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
            pinned_session = SanitizedSession(openEvidence),
            pinned_session_final = SanitizedSession(runtime.Snapshot()),
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

_ = await supervisor.WaitForOpenAsync();

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
}
catch (Exception exception)
{
    primaryFailure = ExceptionDispatchInfo.Capture(exception);
    supervisor.CapturePrimary(exception);
}
finally
{
    _ = await supervisor.CompleteAsync(primaryFailure?.SourceException);
}
primaryFailure?.Throw();

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}

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
    public string ExpectedRepositoryHead =>
        RequiredSha("--expected-repository-head", 40);
    public string GitExecutable =>
        values.GetValueOrDefault("--git-executable")
        ?? throw new InvalidDataException(
            Arch7bGitExecutableAuthorityContract.ArgumentRequired);
    public string ExpectedGitSha256 =>
        RequiredSha("--expected-git-sha256");
    public string ExpectedGitVersion =>
        Required("--expected-git-version");
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
    public string ExpectedAcquisitionManifestSha256 =>
        RequiredSha("--expected-acquisition-manifest-sha256");
    public string ExpectedBracketContractVersion =>
        Required("--expected-bracket-contract-version");
    public string ExpectedDownloaderVersion =>
        Required("--expected-downloader-version");
    public string ExpectedAccount => Required("--expected-account");
    public string ExpectedSourceSessionId =>
        Required("--expected-source-session-id");
    public string ExpectedExecutionSemanticSha256 =>
        RequiredSha("--expected-execution-semantic-sha256");
    public string ExpectedPositionSemanticSha256 =>
        RequiredSha("--expected-position-semantic-sha256");
    public Guid ExpectedSourceIngestionId =>
        Guid.Parse(Required("--expected-source-ingestion-id"));
    public Guid ExpectedPositionSnapshotId =>
        Guid.Parse(Required("--expected-position-snapshot-id"));
    public string CoreTree => RequiredSha("--core-tree", 40);
    public string IntradayRepositoryCommit =>
        RequiredSha("--intraday-repository-commit", 40);
    public string IntradayTree => RequiredSha("--intraday-tree", 40);
    public string ExpectedRequiredUniverseSha256 =>
        RequiredSha("--expected-required-universe-sha256");
    public DateTimeOffset ExpectedPositionReportP2Utc =>
        DateTimeOffset.Parse(Required("--expected-position-report-p2-utc"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    public int ExpectedNormalizedCount => Integer("--expected-normalized-count");
    public int ExpectedDerivedZeroCount => Integer("--expected-derived-zero-count");
    public int ExpectedUnknownCount => Integer("--expected-unknown-count");
    public string ExpectedConsumerExecutableSha256 =>
        RequiredSha("--expected-consumer-executable-sha256");
    public string SyntheticRunId => Required("--synthetic-run-id");
    public string SyntheticOwnerId => Required("--synthetic-owner-id");
    public string SyntheticFutureAuthorizationId =>
        Required("--synthetic-future-authorization-id");
    public bool HistoricalFixture => flags.Contains("--historical-fixture");
    public bool Apply => flags.Contains("--apply");
    public DateTimeOffset ObservedUtc =>
        DateTimeOffset.Parse(Required("--observed-utc"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    public string ReadyMarkerPath => Required("--ready-marker");
    public string ArmedStatePath => Required("--armed-state");
    public string OwnerLockPath => Required("--owner-lock");
    public string RunId => Required("--run-id");
    public string OwnerId => Required("--owner-id");
    public string FutureAuthorizationId => Required("--future-authorization-id");
    public bool RequiresRepository =>
        Mode is not "qualify-pinned-postgresql-session" and
            not "qualify-database-clock" and
            not "qualify-repository-authority" and
            not "qualify-core-to-position-consumer-offline-bridge" and
            not "qualify-runtime-selection";
    public string LifecycleEvidenceDirectory => Mode switch
    {
        "arm-import" => ParentDirectory(ArmedStatePath),
        "publish-ready" or "apply-import" =>
            ParentDirectory(ReadyMarkerPath),
        _ => Path.GetFullPath(OutputDirectory)
    };

    private static string ParentDirectory(string path) =>
        Path.GetDirectoryName(Path.GetFullPath(path))
        ?? throw new InvalidDataException(
            "ARCH7B_POSITION_IMPORT_LIFECYCLE_ROOT_INVALID");

    public static Arch7bPositionImportArguments Parse(string[] args)
    {
        Require(args.Length > 0 &&
                args[0] is "arm-import" or "publish-ready" or
                    "plan-import" or "apply-import" or "qualify-database-clock" or
                    "qualify-pinned-postgresql-session" or
                    "qualify-repository-authority" or
                    "qualify-runtime-selection" or
                    "resolve-arm-preconditions" or
                    "run-fresh-position-import-fast-path" or
                    "qualify-core-to-position-consumer-offline-bridge",
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
        if (parsed.Mode == "qualify-repository-authority")
        {
            Require(!parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_REPOSITORY_AUTHORITY_QUALIFICATION_FLAGS_INVALID");
            _ = parsed.RepositoryRoot;
            _ = parsed.BuildCommit;
            _ = parsed.ExpectedRepositoryHead;
            _ = parsed.GitExecutable;
            _ = parsed.ExpectedGitSha256;
            _ = parsed.ExpectedGitVersion;
            _ = parsed.OutputDirectory;
            Require(parsed.BuildCommit == parsed.ExpectedRepositoryHead,
                "ARCH7B_POSITION_IMPORT_REPOSITORY_STATE_MISMATCH");
            return parsed;
        }
        if (parsed.Mode == "qualify-runtime-selection")
        {
            Require(!parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_RUNTIME_SELECTION_FLAGS_INVALID");
            _ = parsed.PackageRoot;
            _ = parsed.OutputDirectory;
            _ = parsed.ExpectedAccount;
            _ = parsed.ExpectedTargetFingerprint;
            _ = parsed.ExpectedSourceSessionId;
            _ = parsed.ExpectedSourceIngestionId;
            _ = parsed.ExpectedPositionSnapshotId;
            Require(parsed.ExpectedAccount == "1754288005",
                "ARCH7B_RUNTIME_SELECTION_ACCOUNT_MISMATCH");
            Require(parsed.ExpectedTargetFingerprint ==
                    "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
                "ARCH7B_RUNTIME_SELECTION_TARGET_MISMATCH");
            return parsed;
        }
        if (parsed.Mode == "qualify-core-to-position-consumer-offline-bridge")
        {
            Require(!parsed.Apply && parsed.HistoricalFixture,
                "ARCH7B_BRIDGE_QUALIFICATION_FLAGS_INVALID");
            _ = parsed.OutputDirectory;
            _ = parsed.EvidenceRoot;
            _ = parsed.CoreRepositoryCommit;
            _ = parsed.CoreTree;
            _ = parsed.IntradayRepositoryCommit;
            _ = parsed.IntradayTree;
            _ = parsed.ExpectedEvidenceSha256;
            _ = parsed.ExpectedContractFileSha256;
            _ = parsed.ExpectedFinalIndexSha256;
            _ = parsed.ExpectedAcquisitionManifestSha256;
            _ = parsed.ExpectedBracketContractVersion;
            _ = parsed.ExpectedDownloaderVersion;
            _ = parsed.ExpectedAccount;
            _ = parsed.ExpectedSourceSessionId;
            _ = parsed.ExpectedExecutionSemanticSha256;
            _ = parsed.ExpectedPositionSemanticSha256;
            _ = parsed.ExpectedSourceIngestionId;
            _ = parsed.ExpectedRequiredUniverseSha256;
            Require(parsed.ExpectedPositionReportP2Utc.Offset == TimeSpan.Zero,
                "ARCH7B_BRIDGE_P2_NOT_UTC");
            _ = parsed.ExpectedNormalizedCount;
            _ = parsed.ExpectedDerivedZeroCount;
            _ = parsed.ExpectedUnknownCount;
            _ = parsed.ExpectedConsumerExecutableSha256;
            _ = parsed.SyntheticRunId;
            _ = parsed.SyntheticOwnerId;
            _ = parsed.SyntheticFutureAuthorizationId;
            return parsed;
        }

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
        Require(parsed.ExpectedTargetFingerprint ==
                "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4",
            "ARCH7B_POSITION_IMPORT_TARGET_FINGERPRINT_MISMATCH");
        if (parsed.Mode == "qualify-pinned-postgresql-session")
        {
            Require(!parsed.Apply && parsed.HistoricalFixture,
                "ARCH7B_PINNED_SESSION_QUALIFICATION_FLAGS_INVALID");
            _ = parsed.PackageRoot;
            _ = parsed.OutputDirectory;
            return parsed;
        }
        else if (parsed.Mode == "qualify-database-clock")
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
            _ = parsed.ExpectedRepositoryHead;
            _ = parsed.GitExecutable;
            _ = parsed.ExpectedGitSha256;
            _ = parsed.ExpectedGitVersion;
            Require(parsed.BuildCommit == parsed.ExpectedRepositoryHead,
                "ARCH7B_POSITION_IMPORT_REPOSITORY_STATE_MISMATCH");
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
        else if (parsed.Mode == "resolve-arm-preconditions")
        {
            Require(!parsed.Apply && !parsed.HistoricalFixture,
                "ARCH7B_PREARM_BINDING_RESOLUTION_FLAGS_INVALID");
            Require(!parsed.values.ContainsKey("--expected-source-ingestion-id"),
                "ARCH7B_PREARM_SOURCE_INGESTION_ARGUMENT_FORBIDDEN");
            _ = parsed.OutputDirectory;
            _ = parsed.RunId;
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

    public Arch7bGitExecutableAuthority BuildGitAuthority() =>
        new Arch7bGitExecutableAuthorityQualifier().Qualify(
            GitExecutable,
            ExpectedGitSha256,
            ExpectedGitVersion,
            Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
            Environment.MachineName);

    public Arch7bPostgreSqlPinnedSession BuildRuntime()
    {
        Arch7bPostgreSqlPinnedTransportProfileContract.ValidateCommandLine(
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
            "resolve-arm-preconditions" =>
                "QQ_ARCH7B_PREARM_BINDING_RESOLVER_READONLY",
            "qualify-pinned-postgresql-session" =>
                "QQ_ARCH7B_PINNED_SESSION_QUALIFICATION_READONLY",
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
        return Arch7bPostgreSqlPinnedSessionFactory.Create(
            Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary,
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
