using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Application;
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
var repositoryAuthority = arguments.Mode == "report-institutional-metric-foundation"
    ? InstitutionalRepositoryStateAuthority.Resolve(
        arguments.RepositoryRoot, arguments.RepositoryCommit)
    : null;
var repositoryCommit = repositoryAuthority?.ActualHead ?? arguments.RepositoryCommit;
var snapshot = await new PmsShadowReadOnlyReportingReader(options, target)
    .ReadAsync(arguments.AsOfUtc, repositoryCommit, arguments.IncludeHistory);
if (repositoryAuthority is not null)
    snapshot = snapshot with { RepositoryAuthority = repositoryAuthority };
Require(snapshot.Database.TransactionReadOnly, "REPORTING_TRANSACTION_NOT_READ_ONLY");
Require(!snapshot.Database.PendingModelChanges, "REPORTING_PENDING_MODEL_CHANGES");
Require(snapshot.Database.Database == arguments.ExpectedDatabase,
    "REPORTING_TARGET_IDENTITY_MISMATCH");
Require(snapshot.Database.PostgreSqlMajor == arguments.ExpectedPostgreSqlMajor,
    "REPORTING_POSTGRESQL_MAJOR_MISMATCH");

if (arguments.Mode == "report-operational-state")
{
    var latestEconomicRevisionId = snapshot.EconomicRevisions
        .Where(value => value.Qualifying)
        .OrderByDescending(value => value.CompletedAtUtc)
        .ThenByDescending(value => value.EconomicRevisionId)
        .Select(value => (Guid?)value.EconomicRevisionId)
        .FirstOrDefault();
    var latestArch7aRevisionId = snapshot.Arch7a
        .Where(value => value.IsAuthoritativeForEconomicRevision &&
                        value.QualificationRunId.HasValue)
        .OrderByDescending(value => value.QualificationCompletedAtUtc)
        .ThenByDescending(value => value.QualificationRunId)
        .Select(value => (Guid?)value.EconomicRevisionId)
        .FirstOrDefault();
    snapshot = snapshot with
    {
        PositionMarketLineage = Arch7bPositionMarketReporting.Load(
            arguments.PositionMarketLineagePath,
            arguments.ExpectedPositionMarketLineageSha256,
            arguments.PositionMarketRevisionBindingPath,
            arguments.ExpectedPositionMarketRevisionBindingSha256,
            latestEconomicRevisionId, latestArch7aRevisionId)
    };
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
    var roadmap = InstitutionalRoadmapAuthority.Resolve(
        arguments.RepositoryRoot, arguments.RoadmapPath);
    var report = InstitutionalMetricProjector.Build(snapshot, roadmap.Sha256);
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
        roadmap_sha256 = roadmap.Sha256,
        roadmap_authority_contract = roadmap.ContractVersion,
        source_snapshot_sha256 = bundle.SourceSnapshotSha256,
        repository_authority_contract = repositoryAuthority!.ContractVersion,
        actual_repository_head = repositoryAuthority.ActualHead,
        repository_worktree_clean = repositoryAuthority.WorktreeClean,
        repository_evidence_sha256 = repositoryAuthority.EvidenceSha256,
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

public sealed class ReportingArguments
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
    public string RepositoryRoot => Required("--repository-root");
    public string? RoadmapPath => values.GetValueOrDefault("--roadmap-path");
    public string PositionMarketLineagePath =>
        Required("--position-market-lineage-path");
    public string ExpectedPositionMarketLineageSha256 =>
        Required("--expected-position-market-lineage-sha256");
    public string PositionMarketRevisionBindingPath =>
        Required("--position-market-revision-binding-path");
    public string ExpectedPositionMarketRevisionBindingSha256 =>
        Required("--expected-position-market-revision-binding-sha256");
    public bool Overwrite { get; }
    public int IncludeHistory => values.TryGetValue("--include-history", out var value)
        ? int.Parse(value, CultureInfo.InvariantCulture)
        : 64;
    public DateTimeOffset AsOfUtc
    {
        get
        {
            if (values.TryGetValue("--as-of-utc", out var value))
                return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            if (Mode == "report-institutional-metric-foundation")
                throw new InvalidDataException("RPT2_EXPLICIT_AS_OF_REQUIRED");
            return DateTimeOffset.UtcNow;
        }
    }

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
        if (parsed.Mode == "report-operational-state")
        {
            Require(Path.IsPathFullyQualified(parsed.PositionMarketLineagePath),
                "REPORTING_POSITION_MARKET_LINEAGE_PATH_NOT_ABSOLUTE");
            Require(Path.IsPathFullyQualified(parsed.PositionMarketRevisionBindingPath),
                "REPORTING_POSITION_MARKET_REVISION_BINDING_PATH_NOT_ABSOLUTE");
            Require(Arch5bHashing.IsSha256(parsed.ExpectedPositionMarketLineageSha256) &&
                    parsed.ExpectedPositionMarketLineageSha256.All(value => !char.IsUpper(value)),
                "REPORTING_POSITION_MARKET_LINEAGE_SHA_INVALID");
            Require(Arch5bHashing.IsSha256(parsed.ExpectedPositionMarketRevisionBindingSha256) &&
                    parsed.ExpectedPositionMarketRevisionBindingSha256.All(value => !char.IsUpper(value)),
                "REPORTING_POSITION_MARKET_REVISION_BINDING_SHA_INVALID");
        }
        if (parsed.Mode == "report-institutional-metric-foundation")
            _ = parsed.RepositoryRoot;
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
