using System.Net;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record PmsShadowPostgreSqlTargetSettings(
    string ExpectedEnvironment,
    string ExpectedDatabase,
    string ExpectedSchema,
    int ExpectedPostgresMajor,
    bool RequireTls,
    bool AllowLoopback,
    string TargetProfileId);

public sealed record PmsShadowPostgreSqlTarget(
    string Host,
    int Port,
    string Database,
    string ExpectedEnvironment,
    string ExpectedSchema,
    int ExpectedPostgresMajor,
    string TargetProfileId,
    string TargetKind,
    string TlsPolicy,
    string TargetFingerprint)
{
    public string ObservableIdentity =>
        $"profile={TargetProfileId};fingerprint={TargetFingerprint};kind={TargetKind};" +
        $"database={Database};postgres_major={ExpectedPostgresMajor};tls={TlsPolicy}";
}

public static class PmsShadowPostgreSqlTargetContract
{
    public const string TestEnvironment = "TEST";
    public const string LoopbackKind = "LOOPBACK";
    public const string RemoteTlsKind = "REMOTE_TLS";

    public static PmsShadowPostgreSqlTarget Validate(
        string connectionString,
        PmsShadowPostgreSqlTargetSettings settings)
    {
        Require(!string.IsNullOrWhiteSpace(connectionString), "POSTGRESQL_CONNECTION_STRING_REQUIRED");
        Require(settings.ExpectedEnvironment == TestEnvironment,
            "POSTGRESQL_TARGET_ENVIRONMENT_NOT_TEST");
        Require(!string.IsNullOrWhiteSpace(settings.ExpectedDatabase),
            "POSTGRESQL_TARGET_EXPECTED_DATABASE_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(settings.ExpectedSchema),
            "POSTGRESQL_TARGET_EXPECTED_SCHEMA_REQUIRED");
        Require(settings.ExpectedSchema == PmsShadowStateContract.SchemaName,
            "POSTGRESQL_TARGET_SCHEMA_MISMATCH");
        Require(settings.ExpectedPostgresMajor is >= 10 and <= 99,
            "POSTGRESQL_TARGET_EXPECTED_MAJOR_INVALID");
        Require(!string.IsNullOrWhiteSpace(settings.TargetProfileId),
            "POSTGRESQL_TARGET_PROFILE_REQUIRED");
        RejectProductionIdentity(settings.ExpectedDatabase, "POSTGRESQL_TARGET_DATABASE_PRODUCTION_FORBIDDEN");
        RejectProductionIdentity(settings.TargetProfileId, "POSTGRESQL_TARGET_PROFILE_PRODUCTION_FORBIDDEN");

        NpgsqlConnectionStringBuilder identity;
        try
        {
            identity = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidDataException("POSTGRESQL_CONNECTION_STRING_INVALID");
        }

        var host = NormalizeHost(identity.Host);
        Require(host.Length > 0, "POSTGRESQL_TARGET_HOST_REQUIRED");
        Require(!host.Contains(','), "POSTGRESQL_TARGET_MULTIPLE_HOSTS_FORBIDDEN");
        Require(identity.Port is > 0 and <= 65535, "POSTGRESQL_TARGET_PORT_INVALID");
        Require(!string.IsNullOrWhiteSpace(identity.Database), "POSTGRESQL_TARGET_DATABASE_REQUIRED");
        Require(identity.Database == settings.ExpectedDatabase,
            "POSTGRESQL_TARGET_DATABASE_MISMATCH");
        Require(!string.IsNullOrWhiteSpace(identity.Username),
            "POSTGRESQL_TARGET_USERNAME_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(identity.Password),
            "POSTGRESQL_TARGET_CREDENTIAL_REQUIRED");

#pragma warning disable CS0618 // Parsed only to reject legacy certificate-validation bypass.
        var trustServerCertificate = identity.TrustServerCertificate;
#pragma warning restore CS0618
        var database = identity.Database!;

        var loopback = IsLoopback(host);
        if (loopback)
            Require(settings.AllowLoopback, "POSTGRESQL_TARGET_LOOPBACK_NOT_ALLOWED");
        else
        {
            Require(settings.RequireTls, "POSTGRESQL_TARGET_REMOTE_TLS_REQUIRED");
            Require(identity.SslMode == SslMode.VerifyFull,
                "POSTGRESQL_TARGET_REMOTE_VERIFY_FULL_REQUIRED");
            Require(!trustServerCertificate,
                "POSTGRESQL_TARGET_REMOTE_TRUST_SERVER_CERTIFICATE_FORBIDDEN");
        }

        if (settings.RequireTls)
        {
            Require(identity.SslMode == SslMode.VerifyFull,
                "POSTGRESQL_TARGET_VERIFY_FULL_REQUIRED");
            Require(!trustServerCertificate,
                "POSTGRESQL_TARGET_TRUST_SERVER_CERTIFICATE_FORBIDDEN");
        }

        var kind = loopback ? LoopbackKind : RemoteTlsKind;
        var tlsPolicy = identity.SslMode.ToString().ToUpperInvariant();
        var fingerprint = Fingerprint(host, identity.Port, database,
            settings.ExpectedEnvironment, settings.ExpectedSchema, settings.ExpectedPostgresMajor,
            settings.TargetProfileId, kind, tlsPolicy);
        return new(host, identity.Port, database, settings.ExpectedEnvironment,
            settings.ExpectedSchema, settings.ExpectedPostgresMajor, settings.TargetProfileId,
            kind, tlsPolicy, fingerprint);
    }

    public static string Fingerprint(
        string host,
        int port,
        string database,
        string environment,
        string schema,
        int expectedPostgresMajor,
        string profileId,
        string targetKind,
        string tlsPolicy)
    {
        var material = FingerprintMaterial(host, port, database, environment, schema,
            expectedPostgresMajor, profileId, targetKind, tlsPolicy);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    public static string FingerprintMaterial(
        string host,
        int port,
        string database,
        string environment,
        string schema,
        int expectedPostgresMajor,
        string profileId,
        string targetKind,
        string tlsPolicy) =>
        string.Join('\n',
            $"host={NormalizeHost(host)}",
            $"port={port}",
            $"database={database}",
            $"environment={environment}",
            $"schema={schema}",
            $"profile={profileId}",
            $"kind={targetKind}",
            $"postgres_major={expectedPostgresMajor}",
            $"tls={tlsPolicy.ToUpperInvariant()}");

    private static string NormalizeHost(string? value) =>
        (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    private static bool IsLoopback(string host) =>
        host == "localhost" ||
        IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    private static void RejectProductionIdentity(string value, string code)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == "prod" || normalized.StartsWith("prod_", StringComparison.Ordinal) ||
            normalized.EndsWith("_prod", StringComparison.Ordinal) ||
            normalized.Contains("production", StringComparison.Ordinal) ||
            normalized.Contains("921640160", StringComparison.Ordinal))
            throw new InvalidDataException(code);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
