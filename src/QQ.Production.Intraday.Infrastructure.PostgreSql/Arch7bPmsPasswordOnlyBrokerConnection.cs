using System.Security.Cryptography;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bPmsPasswordOnlyBrokerConnectionSettings(
    string Host,
    int Port,
    string Database,
    string Username,
    string ExpectedEnvironment,
    string ExpectedSchema,
    int ExpectedPostgreSqlMajor,
    string TargetProfileId,
    string ExpectedTargetFingerprint,
    string RootCertificatePath,
    string ExpectedRootCertificateSha256,
    bool RequireTls,
    bool AllowLoopback,
    bool Pooling,
    bool Enlist,
    bool Multiplexing);

public sealed class Arch7bPmsPasswordOnlyBrokerConnection : IDisposable
{
    public const string ContractVersion = "arch7b_pms_password_only_broker_connection_contract_v1";
    public const string PasswordEnvironmentVariable = "QQ_ARCH7B_POSITION_IMPORT_FAST_PATH";
    public const string LegacyConnectionEnvironmentVariable =
        "QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING";

    private string connectionString;

    private Arch7bPmsPasswordOnlyBrokerConnection(string connectionString,
        PmsShadowPostgreSqlTarget target)
    {
        this.connectionString = connectionString;
        Target = target;
    }

    public string ConnectionString => connectionString.Length > 0
        ? connectionString
        : throw new ObjectDisposedException(nameof(Arch7bPmsPasswordOnlyBrokerConnection));
    public PmsShadowPostgreSqlTarget Target { get; }

    public static Arch7bPmsPasswordOnlyBrokerConnection Create(
        Arch7bPmsPasswordOnlyBrokerConnectionSettings settings,
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        Require(string.IsNullOrWhiteSpace(environment(LegacyConnectionEnvironmentVariable)),
            "ARCH7B_PMS_BROKER_FULL_CONNECTION_STRING_FORBIDDEN");
        var password = environment(PasswordEnvironmentVariable);
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7B_PMS_BROKER_PASSWORD_ENV_REQUIRED");
        Require(!LooksLikeConnectionString(password!),
            "ARCH7B_PMS_BROKER_PASSWORD_ONLY_REQUIRED");
        ValidateSettings(settings);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            Password = password,
            SslMode = SslMode.VerifyFull,
            RootCertificate = settings.RootCertificatePath,
            Pooling = settings.Pooling,
            Enlist = settings.Enlist,
            Multiplexing = settings.Multiplexing
        };
        password = string.Empty;
        var target = PmsShadowPostgreSqlTargetContract.Validate(builder.ConnectionString,
            new(settings.ExpectedEnvironment, settings.Database, settings.ExpectedSchema,
                settings.ExpectedPostgreSqlMajor, settings.RequireTls, settings.AllowLoopback,
                settings.TargetProfileId));
        Require(target.Host == settings.Host.Trim().TrimEnd('.').ToLowerInvariant(),
            "ARCH7B_PMS_BROKER_TARGET_HOST_MISMATCH");
        Require(target.Database == settings.Database,
            "ARCH7B_PMS_BROKER_TARGET_DATABASE_MISMATCH");
        Require(target.TlsPolicy == "VERIFYFULL",
            "ARCH7B_PMS_BROKER_TARGET_TLS_MISMATCH");
        Require(target.TargetFingerprint == settings.ExpectedTargetFingerprint,
            "ARCH7B_PMS_BROKER_TARGET_FINGERPRINT_MISMATCH");
        return new(builder.ConnectionString, target);
    }

    public void Dispose() => connectionString = string.Empty;

    private static void ValidateSettings(Arch7bPmsPasswordOnlyBrokerConnectionSettings settings)
    {
        Require(settings.TargetProfileId == "ARCH7B_RDS_TEST",
            "ARCH7B_PMS_BROKER_TARGET_PROFILE_MISMATCH");
        Require(settings.ExpectedEnvironment == "TEST",
            "ARCH7B_PMS_BROKER_TARGET_ENVIRONMENT_MISMATCH");
        Require(settings.Host == Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint,
            "ARCH7B_PMS_BROKER_TARGET_HOST_MISMATCH");
        Require(settings.Port == 5432, "ARCH7B_PMS_BROKER_TARGET_PORT_MISMATCH");
        Require(settings.Database == "qq_pms_shadow_arch7b_test",
            "ARCH7B_PMS_BROKER_TARGET_DATABASE_MISMATCH");
        Require(settings.Username == "qq_arch7b_position_importer",
            "ARCH7B_PMS_BROKER_TARGET_USERNAME_MISMATCH");
        Require(settings.ExpectedSchema == PmsShadowStateContract.SchemaName,
            "ARCH7B_PMS_BROKER_TARGET_SCHEMA_MISMATCH");
        Require(settings.ExpectedPostgreSqlMajor == 18,
            "ARCH7B_PMS_BROKER_POSTGRESQL_MAJOR_MISMATCH");
        Require(settings.RequireTls && !settings.AllowLoopback,
            "ARCH7B_PMS_BROKER_TARGET_TLS_MISMATCH");
        Require(!settings.Pooling && !settings.Enlist && !settings.Multiplexing,
            "ARCH7B_PMS_BROKER_CONNECTION_POLICY_MISMATCH");
        Require(Path.IsPathFullyQualified(settings.RootCertificatePath) &&
            File.Exists(settings.RootCertificatePath),
            "ARCH7B_PMS_BROKER_ROOT_CA_MISSING");
        var rootSha = Convert.ToHexStringLower(SHA256.HashData(
            File.ReadAllBytes(settings.RootCertificatePath)));
        Require(rootSha == settings.ExpectedRootCertificateSha256,
            "ARCH7B_PMS_BROKER_ROOT_CA_MISMATCH");
        Require(settings.ExpectedTargetFingerprint.Length == 64 &&
            settings.ExpectedTargetFingerprint.All(value => char.IsAsciiHexDigit(value) && !char.IsUpper(value)),
            "ARCH7B_PMS_BROKER_TARGET_FINGERPRINT_MISMATCH");
    }

    private static bool LooksLikeConnectionString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized.Contains("host=", StringComparison.Ordinal) ||
            normalized.Contains("database=", StringComparison.Ordinal) ||
            normalized.Contains("username=", StringComparison.Ordinal) ||
            normalized.Contains("password=", StringComparison.Ordinal) ||
            normalized.Contains(';');
    }

    private static void Require(bool condition, string blocker)
    {
        if (!condition) throw new InvalidDataException(blocker);
    }
}
