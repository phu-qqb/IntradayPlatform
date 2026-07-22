using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

var arguments = Arguments.Parse(args);
if (arguments.Mode == "--verify-evidence")
{
    arguments.RequireEvidenceBoundary();
    var verifiedPackage = Arch6dPmsShadowEvidencePackageReader.Read(
        arguments.Required("--arch6c-evidence-zip"), arguments.Required("--arch6c-evidence-sha256"),
        arguments.Required("--arch6b-evidence-zip"), arguments.Required("--arch6b-evidence-sha256"));
    Write(new { result = "VERIFIED", verifiedPackage.Verification });
    return;
}

arguments.RequireSafetyBoundary();
var connectionString = arguments.BuildConnectionString();
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.SetPostgresVersion(16, 0))
    .Options;

if (arguments.Mode == "--fingerprint")
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT current_database(), current_user, version(),
               (SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = current_database()),
               EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'pms_shadow'),
               (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'pms_shadow'),
               (SELECT ssl FROM pg_stat_ssl WHERE pid = pg_backend_pid())
        """;
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Write(new
    {
        environment = "TEST",
        provider = "PostgreSQL",
        host_logical_name = arguments.HostLogicalName,
        arguments.Port,
        database_name = reader.GetString(0),
        application_role = reader.GetString(1),
        server_version = reader.GetString(2),
        database_owner = reader.GetString(3),
        pms_shadow_schema_present = reader.GetBoolean(4),
        pms_shadow_table_count = reader.GetInt64(5),
        tls = reader.GetBoolean(6),
        secret_reference = arguments.SecretReference,
        production = false,
        real_account_data_present = false,
        approved_target = false
    });
    return;
}

await using var context = new PmsShadowDbContext(options);
if (arguments.Mode == "--apply-migration")
{
    var known = context.Database.GetMigrations().ToArray();
    var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    var pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();
    Require(known.SequenceEqual(PmsShadowStateContract.MigrationIds), "MIGRATION_SET_NOT_EXACT");
    Require(applied.SequenceEqual(Array.Empty<string>()) ||
        applied.SequenceEqual([PmsShadowStateContract.InitialMigrationId]), "APPLIED_MIGRATION_SET_NOT_SAFE");
    var expectedPending = applied.Length == 0
        ? PmsShadowStateContract.MigrationIds
        : [PmsShadowStateContract.CorrectiveMigrationId];
    Require(pending.SequenceEqual(expectedPending), "PENDING_MIGRATION_SET_NOT_EXACT");
    await context.Database.MigrateAsync();
    Write(new { result = "APPLIED", migration_ids = pending });
    return;
}

if (arguments.Mode == "--rollback-migration")
{
    var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    Require(applied.SequenceEqual(PmsShadowStateContract.MigrationIds), "APPLIED_MIGRATION_SET_NOT_EXACT");
    await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
    Write(new { result = "ROLLED_BACK", migration_ids = applied });
    return;
}

Require(arguments.Mode == "--import-arch6b-shadow-session", "UNKNOWN_MODE");
var package = Arch6dPmsShadowEvidencePackageReader.Read(
    arguments.Required("--arch6c-evidence-zip"), arguments.Required("--arch6c-evidence-sha256"),
    arguments.Required("--arch6b-evidence-zip"), arguments.Required("--arch6b-evidence-sha256"));
var factory = new SingleContextFactory(options);
var importer = new Arch6bPmsShadowSessionImporter(new EfPmsShadowSessionImportStore(factory));
var outcome = await importer.ImportAsync(package.Plan,
    new("TEST", true, PmsShadowStateContract.ContractVersion));
Write(new { result = outcome.Result.ToString(), outcome, package.Verification });

static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
}));

static void Require(bool condition, string issue)
{
    if (!condition) throw new InvalidOperationException(issue);
}

sealed class SingleContextFactory(DbContextOptions<PmsShadowDbContext> options) : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);
}

sealed class Arguments
{
    private readonly Dictionary<string, string> values;

    private Arguments(string mode, Dictionary<string, string> values)
    {
        Mode = mode;
        this.values = values;
    }

    public string Mode { get; }
    public string HostLogicalName => Required("--host-logical-name");
    public int Port => int.Parse(Required("--port"), System.Globalization.CultureInfo.InvariantCulture);
    public string SecretReference => Required("--database-secret-ref");

    public static Arguments Parse(string[] args)
    {
        var modes = args.Where(x => x is "--verify-evidence" or "--fingerprint" or "--apply-migration" or
            "--rollback-migration" or "--import-arch6b-shadow-session").ToArray();
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

    public void RequireSafetyBoundary()
    {
        RequireEvidenceBoundary();
        Guard.Require(Required("--provider") == "Npgsql", "POSTGRESQL_PROVIDER_REQUIRED");
        Guard.Require(!Required("--database").Contains("prod", StringComparison.OrdinalIgnoreCase),
            "PRODUCTION_DATABASE_NAME_REJECTED");
    }

    public string Required(string name) =>
        values.GetValueOrDefault(name) ?? throw new ArgumentException($"MISSING_ARGUMENT:{name}");

    public string BuildConnectionString()
    {
        Guard.Require(SecretReference.StartsWith("env:", StringComparison.Ordinal), "SECRET_REFERENCE_MUST_USE_ENV");
        var secretName = SecretReference[4..];
        Guard.Require(secretName.Length > 0, "SECRET_ENV_NAME_MISSING");
        var password = Environment.GetEnvironmentVariable(secretName);
        Guard.Require(!string.IsNullOrEmpty(password), "SECRET_ENV_VALUE_UNAVAILABLE");
        return new NpgsqlConnectionStringBuilder
        {
            Host = Required("--host"),
            Port = Port,
            Database = Required("--database"),
            Username = Required("--role"),
            Password = password,
            ApplicationName = "QQ_ARCH6D_PMS_SHADOW_ADMIN",
            SslMode = Enum.Parse<SslMode>(values.GetValueOrDefault("--ssl-mode") ?? "Prefer", true),
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
