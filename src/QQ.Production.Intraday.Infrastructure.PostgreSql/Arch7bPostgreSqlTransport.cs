using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public enum Arch7bPostgreSqlAccessMode
{
    ReadOnly,
    ApplyAppendOnly
}

public sealed record Arch7bPostgreSqlTransportProfile(
    string ContractVersion,
    string Profile,
    string ExecutionHostInstanceId,
    string ExecutionHostPrivateIp,
    string ExecutionHostSubnetId,
    string ExecutionHostSecurityGroupId,
    string RdsInstanceId,
    string RdsEndpoint,
    int RdsPort,
    string RdsVpcId,
    string RdsSecurityGroupId,
    string RdsIngressRuleId,
    string RunnerEgressRuleId,
    int PostgreSqlMajor,
    string NpgsqlVersion,
    string DotNetRuntimeVersion,
    SslMode SslMode,
    SslNegotiation SslNegotiation,
    GssEncryptionMode GssEncryptionMode,
    string CertificateValidationMode,
    bool Pooling,
    int MinimumPoolSize,
    int MaximumPoolSize,
    bool Multiplexing,
    bool NoResetOnClose,
    bool Enlist,
    int ConnectionTimeoutSeconds,
    int CommandTimeoutSeconds,
    int ConnectionIdleLifetimeSeconds,
    int ConnectionLifetimeSeconds,
    bool IncludeErrorDetail,
    bool LogParameters,
    bool PersistSecurityInfo,
    string RootCertificateSha256)
{
    public const string Version = "arch7b_postgresql_transport_profile_v1";
    public const string DirectPrimaryProfile =
        "DIRECT_VPC_PRIMARY_HOST_VERIFY_FULL";
    public const string DirectEndpoint =
        "db-arch7b-pms-shadow-test.cx0goossu17s.eu-west-2.rds.amazonaws.com";
    public const string ExpectedRootCertificateSha256 =
        "17976078e32d253e3d77a464933d96804357a7d61206e0ecdd38145a64f67527";
    public const int ColdConnectionTimeoutSeconds = 20;

    public static Arch7bPostgreSqlTransportProfile DirectPrimary { get; } =
        new(
            Version,
            DirectPrimaryProfile,
            "i-05535ebe6ce80a57b",
            "10.0.2.94",
            "subnet-06a16e14d266882ca",
            "sg-0928052822c1b4c5b",
            "db-arch7b-pms-shadow-test",
            DirectEndpoint,
            5432,
            "vpc-0dcdbfc5ec569ac68",
            "sg-08a2c70bc6033a965",
            "sgr-04ae47dda2d9e6938",
            "sgr-06668dd7b02b3f444",
            18,
            "10.0.0",
            "10.0.7",
            SslMode.VerifyFull,
            SslNegotiation.Postgres,
            GssEncryptionMode.Disable,
            "CHAIN_AND_HOSTNAME",
            true,
            1,
            1,
            false,
            false,
            false,
            ColdConnectionTimeoutSeconds,
            30,
            600,
            900,
            false,
            false,
            false,
            ExpectedRootCertificateSha256);

    public string Sha256 => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalMaterial())));

    public string CanonicalMaterial() => string.Join('\n',
        ContractVersion,
        Profile,
        ExecutionHostInstanceId,
        ExecutionHostPrivateIp,
        ExecutionHostSubnetId,
        ExecutionHostSecurityGroupId,
        RdsInstanceId,
        RdsEndpoint,
        RdsPort.ToString(CultureInfo.InvariantCulture),
        RdsVpcId,
        RdsSecurityGroupId,
        RdsIngressRuleId,
        RunnerEgressRuleId,
        PostgreSqlMajor.ToString(CultureInfo.InvariantCulture),
        NpgsqlVersion,
        DotNetRuntimeVersion,
        SslMode.ToString(),
        SslNegotiation.ToString(),
        GssEncryptionMode.ToString(),
        CertificateValidationMode,
        Pooling.ToString(CultureInfo.InvariantCulture),
        MinimumPoolSize.ToString(CultureInfo.InvariantCulture),
        MaximumPoolSize.ToString(CultureInfo.InvariantCulture),
        Multiplexing.ToString(CultureInfo.InvariantCulture),
        NoResetOnClose.ToString(CultureInfo.InvariantCulture),
        Enlist.ToString(CultureInfo.InvariantCulture),
        ConnectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ConnectionIdleLifetimeSeconds.ToString(CultureInfo.InvariantCulture),
        ConnectionLifetimeSeconds.ToString(CultureInfo.InvariantCulture),
        IncludeErrorDetail.ToString(CultureInfo.InvariantCulture),
        LogParameters.ToString(CultureInfo.InvariantCulture),
        PersistSecurityInfo.ToString(CultureInfo.InvariantCulture),
        RootCertificateSha256);
}

public static class Arch7bPostgreSqlTransportProfileContract
{
    public const string ColdTimeoutMismatch =
        "ARCH7B_POSTGRESQL_COLD_CONNECTION_TIMEOUT_CONTRACT_MISMATCH";

    public static void Validate(
        Arch7bPostgreSqlTransportProfile profile)
    {
        var expected = Arch7bPostgreSqlTransportProfile.DirectPrimary;
        Require(profile.ContractVersion == expected.ContractVersion,
            "ARCH7B_POSTGRESQL_TRANSPORT_CONTRACT_MISMATCH");
        Require(profile.Profile == expected.Profile,
            "ARCH7B_POSTGRESQL_TRANSPORT_PROFILE_MISMATCH");
        Require(profile.ExecutionHostInstanceId ==
                expected.ExecutionHostInstanceId &&
                profile.ExecutionHostPrivateIp ==
                expected.ExecutionHostPrivateIp &&
                profile.ExecutionHostSubnetId ==
                expected.ExecutionHostSubnetId &&
                profile.ExecutionHostSecurityGroupId ==
                expected.ExecutionHostSecurityGroupId,
            "ARCH7B_POSTGRESQL_EXECUTION_HOST_IDENTITY_MISMATCH");
        Require(!IsLoopback(profile.RdsEndpoint),
            "ARCH7B_POSTGRESQL_LOOPBACK_FORBIDDEN");
        Require(profile.RdsInstanceId == expected.RdsInstanceId &&
                profile.RdsEndpoint == expected.RdsEndpoint &&
                profile.RdsVpcId == expected.RdsVpcId &&
                profile.RdsSecurityGroupId == expected.RdsSecurityGroupId &&
                profile.RdsIngressRuleId == expected.RdsIngressRuleId &&
                profile.RunnerEgressRuleId == expected.RunnerEgressRuleId,
            "ARCH7B_POSTGRESQL_DIRECT_RDS_IDENTITY_MISMATCH");
        Require(profile.RdsPort == 5432,
            "ARCH7B_POSTGRESQL_DIRECT_PORT_MISMATCH");
        Require(profile.PostgreSqlMajor == 18 &&
                profile.NpgsqlVersion == "10.0.0" &&
                profile.DotNetRuntimeVersion == "10.0.7",
            "ARCH7B_POSTGRESQL_RUNTIME_PROFILE_MISMATCH");
        Require(profile.SslMode == SslMode.VerifyFull,
            "ARCH7B_POSTGRESQL_VERIFY_FULL_REQUIRED");
        Require(profile.SslNegotiation == SslNegotiation.Postgres,
            "ARCH7B_POSTGRESQL_POSTGRES_NEGOTIATION_REQUIRED");
        Require(profile.GssEncryptionMode == GssEncryptionMode.Disable,
            "ARCH7B_POSTGRESQL_GSS_DISABLE_REQUIRED");
        Require(profile.CertificateValidationMode == "CHAIN_AND_HOSTNAME",
            "ARCH7B_POSTGRESQL_CERTIFICATE_VALIDATION_MISMATCH");
        Require(profile.Pooling,
            "ARCH7B_POSTGRESQL_POOLING_REQUIRED");
        Require(profile.MinimumPoolSize == 1,
            "ARCH7B_POSTGRESQL_MINIMUM_POOL_SIZE_MISMATCH");
        Require(profile.MaximumPoolSize == 1,
            "ARCH7B_POSTGRESQL_MAXIMUM_POOL_SIZE_MISMATCH");
        Require(!profile.Multiplexing,
            "ARCH7B_POSTGRESQL_MULTIPLEXING_FORBIDDEN");
        Require(!profile.NoResetOnClose,
            "ARCH7B_POSTGRESQL_NO_RESET_ON_CLOSE_FORBIDDEN");
        Require(!profile.Enlist,
            "ARCH7B_POSTGRESQL_ENLIST_FORBIDDEN");
        Require(profile.ConnectionTimeoutSeconds ==
                Arch7bPostgreSqlTransportProfile
                    .ColdConnectionTimeoutSeconds,
            ColdTimeoutMismatch);
        Require(profile.CommandTimeoutSeconds == 30 &&
                profile.ConnectionIdleLifetimeSeconds == 600 &&
                profile.ConnectionLifetimeSeconds == 900,
            "ARCH7B_POSTGRESQL_BOUNDED_LIFETIME_PROFILE_MISMATCH");
        Require(!profile.IncludeErrorDetail &&
                !profile.LogParameters &&
                !profile.PersistSecurityInfo,
            "ARCH7B_POSTGRESQL_SECRET_OBSERVABILITY_FORBIDDEN");
        Require(profile.RootCertificateSha256 ==
                Arch7bPostgreSqlTransportProfile
                    .ExpectedRootCertificateSha256,
            "ARCH7B_POSTGRESQL_ROOT_CA_SHA256_MISMATCH");
    }

    public static void ValidateCommandLine(
        IEnumerable<string> argumentNames,
        string host,
        int port)
    {
        var names = argumentNames.ToHashSet(StringComparer.Ordinal);
        Require(!names.Contains("--connect-host"),
            "ARCH7B_POSTGRESQL_CONNECT_HOST_OVERRIDE_FORBIDDEN");
        Require(!names.Contains("--connect-port"),
            "ARCH7B_POSTGRESQL_CONNECT_PORT_OVERRIDE_FORBIDDEN");
        Require(!names.Contains("--target-host"),
            "ARCH7B_POSTGRESQL_TARGET_HOST_OVERRIDE_FORBIDDEN");
        Require(!names.Contains("--relay") &&
                !names.Contains("--ssm") &&
                !names.Contains("--session-manager-plugin") &&
                !names.Contains("--portproxy"),
            "ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN");
        Require(host == Arch7bPostgreSqlTransportProfile.DirectEndpoint,
            "ARCH7B_POSTGRESQL_DIRECT_ENDPOINT_MISMATCH");
        Require(port == 5432,
            "ARCH7B_POSTGRESQL_DIRECT_PORT_MISMATCH");
    }

    public static void ValidateRootCertificateSha256(string observed)
    {
        Require(string.Equals(observed,
                Arch7bPostgreSqlTransportProfile
                    .ExpectedRootCertificateSha256,
                StringComparison.Ordinal),
            "ARCH7B_POSTGRESQL_ROOT_CA_SHA256_MISMATCH");
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host == "127.0.0.1" ||
        host == "::1";

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7bPostgreSqlWarmConnectionEvidence(
    string ContractVersion,
    DateTimeOffset ColdOpenStartedUtc,
    DateTimeOffset ColdOpenCompletedUtc,
    double ColdOpenElapsedMilliseconds,
    int DatabaseBackendProcessId,
    string DatabaseEndpoint,
    string TlsProfileIdentity,
    string TransportProfileSha256,
    bool WarmedBeforeCriticalDatabaseWork,
    int ColdPhysicalOpenCount,
    int PhysicalReconnectCount,
    int LogicalCheckoutCount,
    int MaximumConcurrentLogicalConnections,
    string EvidenceSha256);

public sealed class Arch7bPostgreSqlWarmConnectionAuthority
{
    public const string Version =
        "arch7b_postgresql_warm_connection_authority_v1";
    public const string PhysicalReconnect =
        "ARCH7B_POST_P2_DATABASE_PHYSICAL_RECONNECT_DETECTED";
    public const string ConcurrentCheckout =
        "ARCH7B_POSTGRESQL_CONCURRENT_LOGICAL_CHECKOUT_FORBIDDEN";
    public const string SlowPooledCheckout =
        "ARCH7B_POSTGRESQL_POOLED_CHECKOUT_SLO_EXCEEDED";

    private readonly object sync = new();
    private readonly Arch7bPostgreSqlTransportProfile profile;
    private DateTimeOffset coldOpenStartedUtc;
    private DateTimeOffset coldOpenCompletedUtc;
    private double coldOpenElapsedMilliseconds;
    private int backendProcessId;
    private int activeLogicalConnections;
    private int maximumConcurrentLogicalConnections;
    private int logicalCheckoutCount;
    private int physicalReconnectCount;
    private bool warmed;
    private bool postP2;

    public Arch7bPostgreSqlWarmConnectionAuthority(
        Arch7bPostgreSqlTransportProfile profile)
    {
        Arch7bPostgreSqlTransportProfileContract.Validate(profile);
        this.profile = profile;
    }

    public bool IsWarmed
    {
        get
        {
            lock (sync) return warmed;
        }
    }

    public void ObserveColdOpen(
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        TimeSpan elapsed,
        int processId)
    {
        lock (sync)
        {
            Require(!warmed,
                "ARCH7B_POSTGRESQL_COLD_OPEN_ALREADY_OBSERVED");
            Require(elapsed <= TimeSpan.FromSeconds(
                    Arch7bPostgreSqlTransportProfile
                        .ColdConnectionTimeoutSeconds),
                "ARCH7B_POSTGRESQL_FIRST_AUTHENTICATED_CONNECTION_SLO_EXCEEDED");
            Require(processId > 0,
                "ARCH7B_POSTGRESQL_BACKEND_PROCESS_ID_INVALID");
            coldOpenStartedUtc = startedUtc;
            coldOpenCompletedUtc = completedUtc;
            coldOpenElapsedMilliseconds = elapsed.TotalMilliseconds;
            backendProcessId = processId;
            warmed = true;
            logicalCheckoutCount = 1;
            activeLogicalConnections = 1;
            maximumConcurrentLogicalConnections = 1;
        }
    }

    public void CompleteColdLogicalCheckout()
    {
        lock (sync)
        {
            Require(warmed && activeLogicalConnections == 1,
                "ARCH7B_POSTGRESQL_COLD_CHECKOUT_STATE_INVALID");
            activeLogicalConnections = 0;
        }
    }

    public void EnterPostP2CriticalPath()
    {
        lock (sync)
        {
            Require(warmed,
                "ARCH7B_POSTGRESQL_WARM_CONNECTION_REQUIRED_BEFORE_P2");
            postP2 = true;
        }
    }

    public void ObserveLogicalCheckout(int processId, TimeSpan elapsed)
    {
        lock (sync)
        {
            Require(warmed,
                "ARCH7B_POSTGRESQL_LOGICAL_CHECKOUT_BEFORE_WARMUP");
            Require(activeLogicalConnections == 0,
                ConcurrentCheckout);
            Require(elapsed <= TimeSpan.FromMilliseconds(1000),
                SlowPooledCheckout);
            if (processId != backendProcessId)
            {
                physicalReconnectCount++;
                if (postP2) throw new InvalidDataException(PhysicalReconnect);
                throw new InvalidDataException(
                    "ARCH7B_POSTGRESQL_WARM_BACKEND_PROCESS_ID_MISMATCH");
            }
            logicalCheckoutCount++;
            activeLogicalConnections = 1;
            maximumConcurrentLogicalConnections = Math.Max(
                maximumConcurrentLogicalConnections,
                activeLogicalConnections);
        }
    }

    public void CompleteLogicalCheckout()
    {
        lock (sync)
        {
            Require(activeLogicalConnections == 1,
                "ARCH7B_POSTGRESQL_LOGICAL_CHECKOUT_STATE_INVALID");
            activeLogicalConnections = 0;
        }
    }

    public Arch7bPostgreSqlWarmConnectionEvidence Snapshot()
    {
        lock (sync)
        {
            Require(warmed,
                "ARCH7B_POSTGRESQL_WARM_CONNECTION_EVIDENCE_UNAVAILABLE");
            var material = JsonSerializer.Serialize(new
            {
                contract_version = Version,
                cold_open_started_utc = coldOpenStartedUtc,
                cold_open_completed_utc = coldOpenCompletedUtc,
                cold_open_elapsed_milliseconds =
                    coldOpenElapsedMilliseconds,
                database_backend_process_id = backendProcessId,
                database_endpoint = profile.RdsEndpoint,
                tls_profile_identity =
                    "VERIFY_FULL|POSTGRES|GSS_DISABLE|CHAIN_AND_HOSTNAME",
                transport_profile_sha256 = profile.Sha256,
                warmed_before_critical_database_work = true,
                cold_physical_open_count = 1,
                physical_reconnect_count = physicalReconnectCount,
                logical_checkout_count = logicalCheckoutCount,
                maximum_concurrent_logical_connections =
                    maximumConcurrentLogicalConnections
            });
            var sha = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            return new(
                Version,
                coldOpenStartedUtc,
                coldOpenCompletedUtc,
                coldOpenElapsedMilliseconds,
                backendProcessId,
                profile.RdsEndpoint,
                "VERIFY_FULL|POSTGRES|GSS_DISABLE|CHAIN_AND_HOSTNAME",
                profile.Sha256,
                true,
                1,
                physicalReconnectCount,
                logicalCheckoutCount,
                maximumConcurrentLogicalConnections,
                sha);
        }
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed class Arch7bPostgreSqlRuntime : IAsyncDisposable
{
    private static int processDataSourceCount;
    private readonly string expectedUsername;
    private int warmStarted;
    private int disposed;

    internal Arch7bPostgreSqlRuntime(
        NpgsqlDataSource dataSource,
        PmsShadowPostgreSqlTarget target,
        Arch7bPostgreSqlTransportProfile profile,
        string expectedUsername)
    {
        if (Interlocked.CompareExchange(
                ref processDataSourceCount, 1, 0) != 0)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_MULTIPLE_DATA_SOURCES_FORBIDDEN");
        DataSource = dataSource;
        Target = target;
        Profile = profile;
        this.expectedUsername = expectedUsername;
        Authority = new(profile);
    }

    public NpgsqlDataSource DataSource { get; }
    public PmsShadowPostgreSqlTarget Target { get; }
    public Arch7bPostgreSqlTransportProfile Profile { get; }
    public Arch7bPostgreSqlWarmConnectionAuthority Authority { get; }

    public async Task<Arch7bPostgreSqlWarmConnectionEvidence> WarmAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref warmStarted, 1) != 0)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_COLD_OPEN_MUST_RUN_ONCE");
        var startedUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        await using var connection =
            await DataSource.OpenConnectionAsync(cancellationToken);
        stopwatch.Stop();
        var completedUtc = DateTimeOffset.UtcNow;
        Authority.ObserveColdOpen(
            startedUtc, completedUtc, stopwatch.Elapsed,
            connection.ProcessID);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT current_database(),
                       current_user,
                       current_setting('server_version_num'),
                       current_setting('TimeZone'),
                       clock_timestamp(),
                       COALESCE((
                           SELECT ssl FROM pg_catalog.pg_stat_ssl
                           WHERE pid = pg_catalog.pg_backend_pid()), false)
                """;
            await using var reader =
                await command.ExecuteReaderAsync(
                    CommandBehavior.SingleRow, cancellationToken);
            Require(await reader.ReadAsync(cancellationToken),
                "ARCH7B_POSTGRESQL_WARM_IDENTITY_ROW_MISSING");
            Require(reader.GetString(0) ==
                    Arch7bBracketedGlobalFlatContract.TargetDatabase,
                "ARCH7B_POSTGRESQL_WARM_DATABASE_MISMATCH");
            Require(reader.GetString(1) == expectedUsername,
                "ARCH7B_POSTGRESQL_WARM_USER_MISMATCH");
            Require(int.Parse(reader.GetString(2),
                        CultureInfo.InvariantCulture) / 10000 ==
                    Profile.PostgreSqlMajor,
                "ARCH7B_POSTGRESQL_WARM_VERSION_MISMATCH");
            Require(reader.GetString(3) is "UTC" or "Etc/UTC",
                "ARCH7B_POSTGRESQL_WARM_TIMEZONE_NOT_UTC");
            var databaseUtc = reader.GetValue(4) switch
            {
                DateTime dateTime when dateTime.Kind == DateTimeKind.Utc =>
                    new DateTimeOffset(dateTime),
                DateTimeOffset dateTimeOffset
                    when dateTimeOffset.Offset == TimeSpan.Zero =>
                    dateTimeOffset,
                _ => throw new InvalidDataException(
                    "ARCH7B_POSTGRESQL_WARM_DATABASE_TIME_NOT_UTC")
            };
            Require(databaseUtc.Offset == TimeSpan.Zero,
                "ARCH7B_POSTGRESQL_WARM_DATABASE_TIME_NOT_UTC");
            Require(reader.GetBoolean(5),
                "ARCH7B_POSTGRESQL_TLS_NOT_ACTIVE");
            Require(Target.TargetFingerprint.Length == 64,
                "ARCH7B_POSTGRESQL_WARM_TARGET_FINGERPRINT_INVALID");
        }
        finally
        {
            Authority.CompleteColdLogicalCheckout();
        }
        return Authority.Snapshot();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await DataSource.DisposeAsync();
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public static class Arch7bPostgreSqlDataSourceFactory
{
    public static Arch7bPostgreSqlRuntime Create(
        Arch7bPostgreSqlTransportProfile profile,
        string username,
        string password,
        string applicationName,
        Arch7bPostgreSqlAccessMode accessMode,
        string rootCertificatePath)
    {
        Arch7bPostgreSqlTransportProfileContract.Validate(profile);
        Require(!string.IsNullOrWhiteSpace(username),
            "ARCH7B_POSTGRESQL_USERNAME_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7B_POSTGRESQL_PASSWORD_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(applicationName),
            "ARCH7B_POSTGRESQL_APPLICATION_NAME_REQUIRED");
        Require(Environment.Version.ToString() ==
                profile.DotNetRuntimeVersion,
            "ARCH7B_POSTGRESQL_DOTNET_RUNTIME_VERSION_MISMATCH");
        var npgsqlVersion = typeof(NpgsqlConnection).Assembly
            .GetName().Version;
        Require(npgsqlVersion is not null &&
                $"{npgsqlVersion.Major}.{npgsqlVersion.Minor}.{npgsqlVersion.Build}" ==
                profile.NpgsqlVersion,
            "ARCH7B_POSTGRESQL_NPGSQL_RUNTIME_VERSION_MISMATCH");
        Require(File.Exists(rootCertificatePath),
            "ARCH7B_POSTGRESQL_ROOT_CA_FILE_MISSING");
        var rootSha = Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(rootCertificatePath)));
        Arch7bPostgreSqlTransportProfileContract
            .ValidateRootCertificateSha256(rootSha);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = profile.RdsEndpoint,
            Port = profile.RdsPort,
            Database = Arch7bBracketedGlobalFlatContract.TargetDatabase,
            Username = username,
            Password = password,
            ApplicationName = applicationName,
            SslMode = profile.SslMode,
            SslNegotiation = profile.SslNegotiation,
            GssEncryptionMode = profile.GssEncryptionMode,
            RootCertificate = rootCertificatePath,
            Pooling = profile.Pooling,
            MinPoolSize = profile.MinimumPoolSize,
            MaxPoolSize = profile.MaximumPoolSize,
            Multiplexing = profile.Multiplexing,
            NoResetOnClose = profile.NoResetOnClose,
            Enlist = profile.Enlist,
            Timeout = profile.ConnectionTimeoutSeconds,
            CommandTimeout = profile.CommandTimeoutSeconds,
            ConnectionIdleLifetime =
                profile.ConnectionIdleLifetimeSeconds,
            ConnectionLifetime = profile.ConnectionLifetimeSeconds,
            IncludeErrorDetail = profile.IncludeErrorDetail,
            LogParameters = profile.LogParameters,
            PersistSecurityInfo = profile.PersistSecurityInfo,
            Options = accessMode == Arch7bPostgreSqlAccessMode.ReadOnly
                ? "-c TimeZone=UTC -c default_transaction_read_only=on"
                : "-c TimeZone=UTC"
        };
        var target = PmsShadowPostgreSqlTargetContract.Validate(
            builder.ConnectionString,
            new(
                Arch7bBracketedGlobalFlatContract.TargetEnvironment,
                Arch7bBracketedGlobalFlatContract.TargetDatabase,
                PmsShadowStateContract.SchemaName,
                Arch7bBracketedGlobalFlatContract.PostgreSqlMajor,
                RequireTls: true,
                AllowLoopback: false,
                Arch7bBracketedGlobalFlatContract.TargetProfile));
        var dataSource = new NpgsqlDataSourceBuilder(
            builder.ConnectionString).Build();
        try
        {
            return new(dataSource, target, profile, username);
        }
        catch
        {
            dataSource.Dispose();
            throw;
        }
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
