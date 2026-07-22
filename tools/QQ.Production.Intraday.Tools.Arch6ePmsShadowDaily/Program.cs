using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arguments.Parse(args);

if (arguments.Mode == "--intraday-cadence-decision")
{
    Write(PmsShadowIntradayCadenceDecision.Authoritative);
    return;
}

arguments.RequireEvidenceBoundary();

if (arguments.Mode == "--build-daily-handoff")
{
    var package = ReadPackage(arguments);
    var plan = package.Arch6dPackage.Plan;
    var request = new PmsShadowDailyIngestionRequest(
        PmsShadowDailyIngestionContract.Version,
        arguments.Required("--source-gate"),
        arguments.Required("--source-decision"),
        plan.Ingestion.SourceSessionId,
        plan.AccountSnapshot.ReportDate,
        package.EvidenceManifestSha256,
        package.Arch6dPackage.Verification.Arch6bEvidenceSha256,
        plan.RowsetSha256,
        plan.ModelRuns.Select(value => value.CoreMasterCommitId).Distinct(StringComparer.Ordinal).Single(),
        plan.ModelRuns.Select(value => value.CoreMasterObjectFormat).Distinct(StringComparer.Ordinal).Single(),
        arguments.Required("--intraday-master-commit-id"),
        arguments.Required("--intraday-master-object-format"),
        plan.QubesInputSnapshots.Select(value => value.SnapshotId).Order().ToArray(),
        plan.ModelRuns.Select(value => value.ModelRunId).Order().ToArray(),
        EfPmsShadowSessionImportStore.ExpectedRowCounts(plan),
        true, true, true, true, true, true,
        "TEST", PmsShadowStateContract.EvidenceClassification, true,
        DateTimeOffset.Parse(arguments.Required("--created-at-utc"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        PmsShadowDailyIngestionContract.CreateIdempotencyKey(plan.Ingestion.SourceSessionId,
            package.Arch6dPackage.Verification.Arch6bEvidenceSha256));
    var validation = PmsShadowDailyHandoffValidator.Validate(request, package);
    Guard.Require(validation.IsValid, string.Join(';', validation.Issues));
    Write(request);
    return;
}

arguments.RequireDatabaseBoundary();
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(arguments.BuildConnectionString(), npgsql => npgsql.SetPostgresVersion(16, 0))
    .Options;
var factory = new SingleContextFactory(options);

if (arguments.Mode == "--coordinate-daily-ingestion")
{
    var request = PmsShadowDailyHandoffSerializer.Read(arguments.Required("--handoff-json"));
    var package = ReadPackage(arguments);
    var coordinator = new PmsShadowDailyIngestionCoordinator(
        new Arch6bPmsShadowSessionImporter(new EfPmsShadowSessionImportStore(factory)));
    Write(await coordinator.CoordinateAsync(request, package));
    return;
}

var policy = new PmsShadowFreshnessPolicy(
    DateOnly.ParseExact(arguments.Required("--operational-date"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
    TimeSpan.FromHours(double.Parse(arguments.Required("--freshness-max-age-hours"),
        CultureInfo.InvariantCulture)));
var nowUtc = DateTimeOffset.Parse(arguments.Required("--now-utc"), CultureInfo.InvariantCulture,
    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
var reads = new EfPmsShadowOperationalReadService(factory);
var sessionId = arguments.Optional("--source-session-id");
var snapshot = sessionId is null
    ? await reads.GetLatestAsync(policy, nowUtc)
    : await reads.GetSessionAsync(sessionId, policy, nowUtc);

if (arguments.Mode == "--pms-shadow-intraday")
{
    var intraday = new EfPmsShadowIntradayReadService(
        new EfPmsShadowIntradaySlotStore(factory), reads);
    Write(await intraday.GetAsync(nowUtc));
    return;
}

if (snapshot is null)
{
    Write(new
    {
        status = PmsShadowFreshnessStatus.MissingToday,
        source_session_id = sessionId,
        operational_date = policy.ExpectedOperationalDate,
        alert = "DAILY_SESSION_MISSING",
        no_order = true
    });
    return;
}

Write(arguments.Mode switch
{
    "--pms-shadow-latest" => snapshot.LatestSession,
    "--pms-shadow-session" => snapshot,
    "--pms-shadow-targets" => snapshot.TargetPositions,
    "--pms-shadow-drifts" => new { snapshot.PositionOnlyDrifts, snapshot.BrokerAdjustedDrifts },
    "--pms-shadow-lineage" => snapshot.Lineage,
    "--pms-shadow-health" => new { snapshot.Freshness, snapshot.Alerts, snapshot.LatestSession.NoOrder },
    _ => throw new InvalidOperationException("UNKNOWN_MODE")
});

static PmsShadowDailyEvidencePackage ReadPackage(Arguments arguments) =>
    PmsShadowDailyEvidencePackageReader.Read(arguments.Required("--arch6c-evidence-zip"),
        arguments.Required("--arch6c-evidence-sha256"), arguments.Required("--arch6b-evidence-zip"),
        arguments.Required("--arch6b-evidence-sha256"));

static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
}));

sealed class SingleContextFactory(DbContextOptions<PmsShadowDbContext> options)
    : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);
}

sealed class Arguments
{
    private static readonly string[] Modes =
    [
        "--build-daily-handoff", "--coordinate-daily-ingestion", "--pms-shadow-latest",
        "--pms-shadow-session", "--pms-shadow-targets", "--pms-shadow-drifts",
        "--pms-shadow-lineage", "--pms-shadow-health", "--pms-shadow-intraday",
        "--intraday-cadence-decision"
    ];
    private readonly Dictionary<string, string> values;

    private Arguments(string mode, Dictionary<string, string> values)
    {
        Mode = mode;
        this.values = values;
    }

    public string Mode { get; }

    public static Arguments Parse(string[] args)
    {
        var modes = args.Where(Modes.Contains).ToArray();
        Guard.Require(modes.Length == 1, "EXACTLY_ONE_MODE_REQUIRED");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == modes[0] || args[index] == "--no-order") continue;
            Guard.Require(args[index].StartsWith("--", StringComparison.Ordinal) && index + 1 < args.Length,
                "ARGUMENTS_MUST_BE_NAME_VALUE_PAIRS");
            values.Add(args[index], args[++index]);
        }
        return new(modes[0], values);
    }

    public void RequireEvidenceBoundary()
    {
        Guard.Require(Required("--environment") == "TEST", "PMS_SHADOW_IMPORT_REQUIRES_TEST_ENVIRONMENT");
        Guard.Require(Environment.GetCommandLineArgs().Contains("--no-order", StringComparer.Ordinal),
            "PMS_SHADOW_IMPORT_REQUIRES_NO_ORDER");
        Guard.Require(Required("--schema-contract-version") == PmsShadowStateContract.ContractVersion,
            "PMS_SHADOW_SCHEMA_CONTRACT_VERSION_MISMATCH");
    }

    public void RequireDatabaseBoundary()
    {
        Guard.Require(Required("--provider") == "Npgsql", "POSTGRESQL_PROVIDER_REQUIRED");
        Guard.Require(!Required("--database").Contains("prod", StringComparison.OrdinalIgnoreCase),
            "PRODUCTION_DATABASE_NAME_REJECTED");
        Guard.Require(Required("--database-secret-ref").StartsWith("env:", StringComparison.Ordinal),
            "SECRET_REFERENCE_MUST_USE_ENV");
    }

    public string Required(string name) => values.GetValueOrDefault(name)
        ?? throw new ArgumentException($"MISSING_ARGUMENT:{name}");

    public string? Optional(string name) => values.GetValueOrDefault(name);

    public string BuildConnectionString()
    {
        var secretReference = Required("--database-secret-ref");
        var password = Environment.GetEnvironmentVariable(secretReference[4..]);
        Guard.Require(!string.IsNullOrEmpty(password), "SECRET_ENV_VALUE_UNAVAILABLE");
        return new NpgsqlConnectionStringBuilder
        {
            Host = Required("--host"),
            Port = int.Parse(Required("--port"), CultureInfo.InvariantCulture),
            Database = Required("--database"),
            Username = Required("--role"),
            Password = password,
            ApplicationName = "QQ_ARCH6E_PMS_SHADOW_DAILY",
            SslMode = Enum.Parse<SslMode>(Optional("--ssl-mode") ?? "Prefer", true),
            IncludeErrorDetail = false
        }.ConnectionString;
    }
}


static class Guard
{
    public static void Require(bool condition, string issue)
    {
        if (!condition) throw new InvalidOperationException(issue);
    }
}
