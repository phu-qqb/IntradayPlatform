using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arch7bGlobalFlatArguments.Parse(args);
var expectations = new Arch7bCoreEvidenceExpectations(
    arguments.CoreRepositoryCommit,
    arguments.ExpectedEvidenceSha256,
    arguments.ExpectedContractFileSha256,
    arguments.ExpectedFinalIndexSha256);
var core = Arch7bCoreBracketEvidencePackageReader.Read(
    arguments.EvidenceRoot, expectations);

var logicalConnectionString = arguments.BuildLogicalConnectionString();
var settings = new PmsShadowPostgreSqlTargetSettings(
    Arch7bBracketedGlobalFlatContract.TargetEnvironment,
    Arch7bBracketedGlobalFlatContract.TargetDatabase,
    PmsShadowStateContract.SchemaName,
    Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
    RequireTls: true,
    AllowLoopback: false,
    Arch7bBracketedGlobalFlatContract.TargetProfile);
var target = PmsShadowPostgreSqlTargetContract.Validate(
    logicalConnectionString, settings);
Require(target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
    "ARCH7B_PMS_TARGET_FINGERPRINT_MISMATCH");

await using var dataSource = arguments.BuildDataSource();
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(dataSource, npgsql =>
        npgsql.SetPostgresVersion(
            Arch7bBracketedGlobalFlatContract.PostgreSqlMajor, 0))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .Options;
var universe = await new Arch7bRequiredPmsUniverseReader(options, target)
    .ReadAsync();
var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(core, universe);
var smokeA = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
var smokeB = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
var smokeIsDeterministic = Arch7bGlobalFlatOutputWriter.SerializeSmoke(smokeA)
    .SequenceEqual(Arch7bGlobalFlatOutputWriter.SerializeSmoke(smokeB));
Require(smokeIsDeterministic,
    "ARCH7B_OFFLINE_SMOKE_NONDETERMINISTIC");
var bundle = Arch7bGlobalFlatOutputWriter.Write(
    arguments.OutputDirectory, core, universe, snapshot, smokeA, smokeB);

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
    transaction_read_only = universe.TransactionReadOnly,
    pending_model_changes = universe.PendingModelChanges,
    no_order = true,
    no_fix = true,
    no_database_write = true,
    no_account_api = true,
    no_databento = true
}, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
}));

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}

public sealed class Arch7bGlobalFlatArguments
{
    private readonly IReadOnlyDictionary<string, string> values;

    private Arch7bGlobalFlatArguments(IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
    }

    public string EvidenceRoot => Required("--evidence-root");
    public string OutputDirectory => Required("--output-directory");
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
        Require(args.Contains("consume-bracketed-global-flat", StringComparer.Ordinal),
            "ARCH7B_MODE_REQUIRED");
        Require(args.Contains("--no-order", StringComparer.Ordinal),
            "ARCH7B_NO_ORDER_REQUIRED");
        var flags = new HashSet<string>(StringComparer.Ordinal)
        {
            "consume-bracketed-global-flat",
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
        var parsed = new Arch7bGlobalFlatArguments(values);
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
        return parsed;
    }

    public string BuildLogicalConnectionString() =>
        BuildConnectionString(Required("--host"), Integer("--port"));

    public NpgsqlDataSource BuildDataSource()
    {
        var logicalHost = Required("--host");
        var connectHost = values.GetValueOrDefault("--connect-host") ?? logicalHost;
        var connectPort = values.ContainsKey("--connect-port")
            ? Integer("--connect-port")
            : Integer("--port");
        var builder = new NpgsqlDataSourceBuilder(
            BuildConnectionString(connectHost, connectPort));
        builder.UseSslClientAuthenticationOptionsCallback(options =>
            options.TargetHost = logicalHost);
        return builder.Build();
    }

    private string BuildConnectionString(string host, int port)
    {
        var reference = Required("--connection-secret-reference");
        Require(reference.StartsWith("env:", StringComparison.Ordinal),
            "ARCH7B_SECRET_REFERENCE_MUST_USE_ENV");
        var password = Environment.GetEnvironmentVariable(reference[4..]);
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7B_SECRET_VALUE_UNAVAILABLE");
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = Arch7bBracketedGlobalFlatContract.TargetDatabase,
            Username = Required("--role"),
            Password = password,
            ApplicationName = "QQ_ARCH7B_GLOBAL_FLAT_READONLY",
            SslMode = SslMode.VerifyFull,
            IncludeErrorDetail = false,
            Options = "-c default_transaction_read_only=on"
        };
        if (values.TryGetValue("--root-certificate", out var rootCertificate))
            builder.RootCertificate = rootCertificate;
        return builder.ConnectionString;
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
    private string Required(string name) =>
        values.GetValueOrDefault(name)
        ?? throw new InvalidDataException($"ARCH7B_ARGUMENT_MISSING:{name}");
    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
