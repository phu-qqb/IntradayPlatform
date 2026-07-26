using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.OperationalReporting;

var arguments = ReportingArguments.Parse(args);
var logicalConnectionString = arguments.BuildLogicalConnectionString();
var settings = new PmsShadowPostgreSqlTargetSettings(
    arguments.ExpectedEnvironment,
    arguments.ExpectedDatabase,
    arguments.ExpectedSchema,
    arguments.ExpectedPostgreSqlMajor,
    RequireTls: true,
    AllowLoopback: false,
    arguments.TargetProfileId);
var target = PmsShadowPostgreSqlTargetContract.Validate(logicalConnectionString, settings);
Require(target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
    "REPORTING_TARGET_IDENTITY_MISMATCH");
await using var dataSource = arguments.BuildDataSource();
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(dataSource, npgsql =>
        npgsql.SetPostgresVersion(arguments.ExpectedPostgreSqlMajor, 0))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .Options;
var snapshot = await new PmsShadowReadOnlyReportingReader(options, target)
    .ReadAsync(arguments.AsOfUtc, arguments.RepositoryCommit,
        arguments.IncludeHistory);
Require(snapshot.Database.TransactionReadOnly, "REPORTING_TRANSACTION_NOT_READ_ONLY");
Require(!snapshot.Database.PendingModelChanges, "REPORTING_PENDING_MODEL_CHANGES");
Require(snapshot.Database.Database == arguments.ExpectedDatabase,
    "REPORTING_TARGET_IDENTITY_MISMATCH");
Require(snapshot.Database.PostgreSqlMajor == arguments.ExpectedPostgreSqlMajor,
    "REPORTING_POSTGRESQL_MAJOR_MISMATCH");

if (arguments.Mode == "report-operational-state")
{
    var report = OperationalReportProjector.Build(snapshot);
    var bundle = DeterministicReportingBundleWriter.Write(
        report, arguments.OutputDirectory, arguments.Overwrite);
    WriteResult(new
    {
        result = "ANUBIS_INFX_READONLY_REPORTING_BUNDLE_CREATED",
        target = target.ObservableIdentity,
        transaction_read_only = snapshot.Database.TransactionReadOnly,
        snapshot.Database.TableCount,
        snapshot.Database.RowCount,
        migration_count = snapshot.Database.AppliedMigrations.Count,
        active_break_count = report.Breaks.Count(value =>
            value.Status is OperationalBreakStatus.Active or OperationalBreakStatus.Unknown),
        bundle.OutputDirectory,
        bundle.BundleSha256,
        no_order = true
    });
}
else
{
    var roadmapPath = Path.GetFullPath(arguments.RoadmapPath);
    Require(Path.GetFileName(roadmapPath) ==
            "hedge-fund-institutional-reporting-roadmap-v1.md" &&
            File.Exists(roadmapPath), "RPT2_ROADMAP_MANIFEST_MISSING");
    var roadmapSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(roadmapPath)));
    var report = InstitutionalMetricProjector.Build(snapshot, roadmapSha);
    var bundle = DeterministicInstitutionalMetricBundleWriter.Write(
        report, arguments.OutputDirectory, arguments.Overwrite);
    WriteResult(new
    {
        result = "RPT2_INSTITUTIONAL_METRIC_FOUNDATION_BUNDLE_CREATED",
        target = target.ObservableIdentity,
        transaction_read_only = snapshot.Database.TransactionReadOnly,
        snapshot.Database.TableCount,
        snapshot.Database.RowCount,
        migration_count = snapshot.Database.AppliedMigrations.Count,
        metric_count = report.Catalog.Count,
        blocked_metric_count = report.Availability.Count(value =>
            value.AvailabilityStatus == MetricAvailabilityStatus.BlockedMissingSource),
        bundle.OutputDirectory,
        bundle.BundleSha256,
        roadmap_sha256 = roadmapSha,
        source_snapshot_sha256 = bundle.SourceSnapshotSha256,
        superseded_bundle_sha256 = InstitutionalMetricContract.SupersededBundleSha256,
        no_order = true
    });
}

static void WriteResult(object result) => Console.WriteLine(JsonSerializer.Serialize(result,
    new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    }));

static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}

internal sealed class ReportingArguments
{
    private readonly IReadOnlyDictionary<string, string> values;

    private ReportingArguments(IReadOnlyDictionary<string, string> values, bool overwrite,
        string mode)
    {
        this.values = values;
        Overwrite = overwrite;
        Mode = mode;
    }

    public string Mode { get; }
    public string ExpectedEnvironment => Required("--expected-environment");
    public string ExpectedDatabase => Required("--expected-database");
    public string ExpectedSchema => Required("--expected-schema");
    public int ExpectedPostgreSqlMajor => Integer("--expected-postgresql-major");
    public string TargetProfileId => Required("--target-profile");
    public string ExpectedTargetFingerprint => Required("--expected-target-fingerprint");
    public string OutputDirectory => Required("--output-directory");
    public string RepositoryCommit => Required("--repository-commit");
    public string RoadmapPath => Required("--roadmap-path");
    public bool Overwrite { get; }
    public int IncludeHistory => values.TryGetValue("--include-history", out var value)
        ? int.Parse(value, CultureInfo.InvariantCulture)
        : 64;
    public DateTimeOffset AsOfUtc => values.TryGetValue("--as-of-utc", out var value)
        ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
        : DateTimeOffset.UtcNow;

    public static ReportingArguments Parse(string[] args)
    {
        var modes = new[]
            {
                "report-operational-state",
                "report-institutional-metric-foundation"
            }
            .Where(mode => args.Contains(mode, StringComparer.Ordinal)).ToArray();
        Require(modes.Length == 1, "REPORTING_MODE_REQUIRED");
        Require(args.Contains("--no-order", StringComparer.Ordinal),
            "REPORTING_NO_ORDER_REQUIRED");
        var overwrite = args.Contains("--overwrite", StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal)
            {
                "report-operational-state",
                "report-institutional-metric-foundation",
                "--no-order",
                "--overwrite"
            };
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (flags.Contains(args[index])) continue;
            Require(args[index].StartsWith("--", StringComparison.Ordinal) &&
                    index + 1 < args.Length,
                "REPORTING_ARGUMENTS_MUST_BE_NAME_VALUE_PAIRS");
            values.Add(args[index], args[++index]);
        }
        var parsed = new ReportingArguments(values, overwrite, modes[0]);
        Require(parsed.ExpectedEnvironment == OperationalReportingContract.TestEnvironment,
            "REPORTING_ENVIRONMENT_NOT_TEST");
        Require(parsed.ExpectedSchema == PmsShadowStateContract.SchemaName,
            "REPORTING_SCHEMA_MISMATCH");
        Require(parsed.TargetProfileId == "ARCH7B_RDS_TEST",
            "REPORTING_TARGET_IDENTITY_MISMATCH");
        Require(parsed.ExpectedDatabase == "qq_pms_shadow_arch7b_test",
            "REPORTING_TARGET_IDENTITY_MISMATCH");
        Require(parsed.ExpectedPostgreSqlMajor == 18,
            "REPORTING_POSTGRESQL_MAJOR_MISMATCH");
        Require(parsed.ExpectedTargetFingerprint.Length == 64 &&
                parsed.ExpectedTargetFingerprint.All(char.IsAsciiHexDigit),
            "REPORTING_TARGET_FINGERPRINT_INVALID");
        Require(parsed.IncludeHistory is >= 1 and <= 1000,
            "REPORTING_INCLUDE_HISTORY_OUT_OF_RANGE");
        Require(parsed.AsOfUtc.Offset == TimeSpan.Zero, "REPORTING_AS_OF_NOT_UTC");
        if (parsed.Mode == "report-institutional-metric-foundation")
            Require(Path.GetFileName(parsed.RoadmapPath) ==
                    "hedge-fund-institutional-reporting-roadmap-v1.md",
                "RPT2_ROADMAP_MANIFEST_PATH_INVALID");
        return parsed;
    }

    public string BuildLogicalConnectionString()
        => BuildConnectionString(Required("--host"), Integer("--port"));

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
        var secretReference = Required("--connection-secret-reference");
        Require(secretReference.StartsWith("env:", StringComparison.Ordinal),
            "REPORTING_SECRET_REFERENCE_MUST_USE_ENV");
        var password = Environment.GetEnvironmentVariable(secretReference[4..]);
        Require(!string.IsNullOrWhiteSpace(password), "REPORTING_SECRET_VALUE_UNAVAILABLE");
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = ExpectedDatabase,
            Username = Required("--role"),
            Password = password,
            ApplicationName = "QQ_ANUBIS_INFX_READONLY_REPORTING",
            SslMode = SslMode.VerifyFull,
            IncludeErrorDetail = false,
            Options = "-c default_transaction_read_only=on"
        };
        if (values.TryGetValue("--root-certificate", out var rootCertificate))
            builder.RootCertificate = rootCertificate;
        return builder.ConnectionString;
    }

    private int Integer(string name)
        => int.Parse(Required(name), CultureInfo.InvariantCulture);

    private string Required(string name)
        => values.GetValueOrDefault(name) ??
           throw new InvalidDataException($"REPORTING_ARGUMENT_MISSING:{name}");

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
