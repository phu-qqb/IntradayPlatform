using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bPostgreSqlPinnedTransportProfile(
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
    bool Multiplexing,
    bool Enlist,
    int ConnectionTimeoutSeconds,
    int CommandTimeoutSeconds,
    bool IncludeErrorDetail,
    bool LogParameters,
    bool PersistSecurityInfo,
    string RootCertificateSha256)
{
    public const string Version = "arch7b_postgresql_transport_profile_v2";
    public const string DirectPrimaryProfile =
        "DIRECT_VPC_PRIMARY_HOST_VERIFY_FULL_PINNED_SESSION";
    public const string DirectEndpoint =
        "db-arch7b-pms-shadow-test.cx0goossu17s.eu-west-2.rds.amazonaws.com";
    public const string ExpectedRootCertificateSha256 =
        "17976078e32d253e3d77a464933d96804357a7d61206e0ecdd38145a64f67527";
    public const int ColdConnectionTimeoutSeconds = 20;

    public static Arch7bPostgreSqlPinnedTransportProfile DirectPrimary
    { get; } = new(
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
            false,
            false,
            false,
            ColdConnectionTimeoutSeconds,
            30,
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
        Multiplexing.ToString(CultureInfo.InvariantCulture),
        Enlist.ToString(CultureInfo.InvariantCulture),
        ConnectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        IncludeErrorDetail.ToString(CultureInfo.InvariantCulture),
        LogParameters.ToString(CultureInfo.InvariantCulture),
        PersistSecurityInfo.ToString(CultureInfo.InvariantCulture),
        RootCertificateSha256);
}

public static class Arch7bPostgreSqlPinnedTransportProfileContract
{
    public const string ColdTimeoutMismatch =
        "ARCH7B_POSTGRESQL_COLD_CONNECTION_TIMEOUT_CONTRACT_MISMATCH";

    public static void Validate(
        Arch7bPostgreSqlPinnedTransportProfile profile)
    {
        var expected = Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary;
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
        Require(profile.CertificateValidationMode ==
                "CHAIN_AND_HOSTNAME",
            "ARCH7B_POSTGRESQL_CERTIFICATE_VALIDATION_MISMATCH");
        Require(!profile.Pooling,
            "ARCH7B_POSTGRESQL_PINNED_SESSION_POOLING_FORBIDDEN");
        Require(!profile.Multiplexing,
            "ARCH7B_POSTGRESQL_MULTIPLEXING_FORBIDDEN");
        Require(!profile.Enlist,
            "ARCH7B_POSTGRESQL_ENLIST_FORBIDDEN");
        Require(profile.ConnectionTimeoutSeconds ==
                Arch7bPostgreSqlPinnedTransportProfile
                    .ColdConnectionTimeoutSeconds,
            ColdTimeoutMismatch);
        Require(profile.CommandTimeoutSeconds == 30,
            "ARCH7B_POSTGRESQL_COMMAND_TIMEOUT_MISMATCH");
        Require(!profile.IncludeErrorDetail &&
                !profile.LogParameters &&
                !profile.PersistSecurityInfo,
            "ARCH7B_POSTGRESQL_SECRET_OBSERVABILITY_FORBIDDEN");
        Require(profile.RootCertificateSha256 ==
                Arch7bPostgreSqlPinnedTransportProfile
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
        Require(host ==
                Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint,
            "ARCH7B_POSTGRESQL_DIRECT_ENDPOINT_MISMATCH");
        Require(port == 5432,
            "ARCH7B_POSTGRESQL_DIRECT_PORT_MISMATCH");
    }

    public static void ValidateRootCertificateSha256(string observed)
    {
        Require(string.Equals(observed,
                Arch7bPostgreSqlPinnedTransportProfile
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

public sealed record Arch7bPostgreSqlPinnedSessionEvidence(
    string ContractVersion,
    string TransportProfileVersion,
    DateTimeOffset SessionOpenedAtDiagnosticUtc,
    double ColdOpenElapsedMilliseconds,
    int BackendProcessId,
    string DatabaseEndpoint,
    string DatabaseName,
    string DatabaseUser,
    string PostgreSqlVersion,
    string SessionTimeZone,
    bool TlsActive,
    string TransportProfileSha256,
    int SessionLeaseCount,
    int MaximumConcurrentLeases,
    double MaximumLeaseAcquisitionMilliseconds,
    int TransactionCount,
    int PhysicalOpenCount,
    int PhysicalReconnectCount,
    int CloseCount,
    bool ConnectionLossObserved,
    string ConnectionState,
    string EvidenceSha256);

public sealed class Arch7bPostgreSqlPinnedSessionAuthority
{
    public const string Version =
        "arch7b_postgresql_pinned_session_authority_v1";
    public const string LeaseContention =
        "ARCH7B_POSTGRESQL_PINNED_SESSION_LEASE_CONTENTION";
    public const string SessionLost =
        "ARCH7B_POST_P2_PINNED_DATABASE_SESSION_LOST";
    public const string SecondOpen =
        "ARCH7B_POSTGRESQL_PINNED_SESSION_SECOND_OPEN_FORBIDDEN";
    public const string IdentityRowMissing =
        "ARCH7B_POSTGRESQL_PINNED_IDENTITY_ROW_MISSING";
    public const string DatabaseMismatch =
        "ARCH7B_POSTGRESQL_PINNED_DATABASE_MISMATCH";
    public const string UserMismatch =
        "ARCH7B_POSTGRESQL_PINNED_USER_MISMATCH";
    public const string VersionMismatch =
        "ARCH7B_POSTGRESQL_PINNED_VERSION_MISMATCH";
    public const string BackendProcessIdMismatch =
        "ARCH7B_POSTGRESQL_PINNED_BACKEND_PROCESS_ID_MISMATCH";
    public const string ContextConnectionMismatch =
        "ARCH7B_POSTGRESQL_PINNED_CONTEXT_CONNECTION_MISMATCH";

    private readonly object sync = new();
    private readonly SemaphoreSlim leaseGate = new(1, 1);
    private readonly Arch7bPostgreSqlPinnedTransportProfile profile;
    private bool sessionOpened;
    private bool disposed;
    private bool connectionLossObserved;
    private DateTimeOffset sessionOpenedAtDiagnosticUtc;
    private double coldOpenElapsedMilliseconds;
    private int backendProcessId;
    private string databaseName = "";
    private string databaseUser = "";
    private string postgreSqlVersion = "";
    private string sessionTimeZone = "";
    private bool tlsActive;
    private int leaseCount;
    private int activeLeases;
    private int maximumConcurrentLeases;
    private double maximumLeaseAcquisitionMilliseconds;
    private int transactionCount;
    private int physicalOpenCount;
    private int closeCount;

    public Arch7bPostgreSqlPinnedSessionAuthority(
        Arch7bPostgreSqlPinnedTransportProfile profile)
    {
        Arch7bPostgreSqlPinnedTransportProfileContract.Validate(profile);
        this.profile = profile;
    }

    public int BackendProcessId
    {
        get
        {
            lock (sync) return backendProcessId;
        }
    }

    public void ObserveOpen(
        DateTimeOffset openedAtDiagnosticUtc,
        TimeSpan elapsed,
        int processId,
        string observedDatabase,
        string observedUser,
        string observedPostgreSqlVersion,
        string observedTimeZone,
        bool observedTlsActive)
    {
        lock (sync)
        {
            Require(!sessionOpened && physicalOpenCount == 0, SecondOpen);
            Require(elapsed <= TimeSpan.FromSeconds(
                    Arch7bPostgreSqlPinnedTransportProfile
                        .ColdConnectionTimeoutSeconds),
                "ARCH7B_POSTGRESQL_FIRST_AUTHENTICATED_CONNECTION_SLO_EXCEEDED");
            Require(processId > 0,
                "ARCH7B_POSTGRESQL_BACKEND_PROCESS_ID_INVALID");
            sessionOpenedAtDiagnosticUtc = openedAtDiagnosticUtc;
            coldOpenElapsedMilliseconds = elapsed.TotalMilliseconds;
            backendProcessId = processId;
            databaseName = observedDatabase;
            databaseUser = observedUser;
            postgreSqlVersion = observedPostgreSqlVersion;
            sessionTimeZone = observedTimeZone;
            tlsActive = observedTlsActive;
            physicalOpenCount = 1;
            sessionOpened = true;
        }
    }

    public async Task<Arch7bPostgreSqlPinnedSessionAuthorityLease>
        AcquireAsync(
            Func<ConnectionState> connectionState,
            CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var entered = await leaseGate.WaitAsync(
            TimeSpan.FromMilliseconds(100), cancellationToken);
        stopwatch.Stop();
        if (!entered || stopwatch.Elapsed > TimeSpan.FromMilliseconds(100))
        {
            if (entered) leaseGate.Release();
            throw new InvalidDataException(LeaseContention);
        }

        try
        {
            lock (sync)
            {
                Require(!disposed && sessionOpened,
                    "ARCH7B_POSTGRESQL_PINNED_SESSION_NOT_OPEN");
                if (connectionState() != ConnectionState.Open)
                {
                    connectionLossObserved = true;
                    throw new InvalidDataException(SessionLost);
                }
                activeLeases++;
                leaseCount++;
                maximumConcurrentLeases = Math.Max(
                    maximumConcurrentLeases, activeLeases);
                maximumLeaseAcquisitionMilliseconds = Math.Max(
                    maximumLeaseAcquisitionMilliseconds,
                    stopwatch.Elapsed.TotalMilliseconds);
                return new(this);
            }
        }
        catch
        {
            leaseGate.Release();
            throw;
        }
    }

    public void ObserveTransaction()
    {
        lock (sync)
        {
            Require(activeLeases == 1,
                "ARCH7B_POSTGRESQL_PINNED_TRANSACTION_WITHOUT_LEASE");
            transactionCount++;
        }
    }

    public void ObserveConnectionLoss()
    {
        lock (sync)
        {
            connectionLossObserved = true;
        }
    }

    public void ObserveClose()
    {
        lock (sync)
        {
            Require(sessionOpened && closeCount == 0,
                "ARCH7B_POSTGRESQL_PINNED_SESSION_CLOSE_STATE_INVALID");
            Require(activeLeases == 0,
                "ARCH7B_POSTGRESQL_PINNED_SESSION_CLOSE_WITH_ACTIVE_LEASE");
            closeCount = 1;
            disposed = true;
        }
    }

    public Arch7bPostgreSqlPinnedSessionEvidence Snapshot(
        ConnectionState connectionState)
    {
        lock (sync)
        {
            Require(sessionOpened,
                "ARCH7B_POSTGRESQL_PINNED_SESSION_EVIDENCE_UNAVAILABLE");
            var material = JsonSerializer.Serialize(new
            {
                contract_version = Version,
                transport_profile_version = profile.ContractVersion,
                session_opened_at_diagnostic_utc =
                    sessionOpenedAtDiagnosticUtc,
                cold_open_elapsed_milliseconds =
                    coldOpenElapsedMilliseconds,
                backend_process_id = backendProcessId,
                database_endpoint = profile.RdsEndpoint,
                database_name = databaseName,
                database_user = databaseUser,
                postgresql_version = postgreSqlVersion,
                session_time_zone = sessionTimeZone,
                tls_active = tlsActive,
                transport_profile_sha256 = profile.Sha256,
                session_lease_count = leaseCount,
                maximum_concurrent_leases = maximumConcurrentLeases,
                maximum_lease_acquisition_milliseconds =
                    maximumLeaseAcquisitionMilliseconds,
                transaction_count = transactionCount,
                physical_open_count = physicalOpenCount,
                physical_reconnect_count = 0,
                close_count = closeCount,
                connection_loss_observed = connectionLossObserved,
                connection_state = connectionState.ToString()
            });
            var sha = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            return new(
                Version,
                profile.ContractVersion,
                sessionOpenedAtDiagnosticUtc,
                coldOpenElapsedMilliseconds,
                backendProcessId,
                profile.RdsEndpoint,
                databaseName,
                databaseUser,
                postgreSqlVersion,
                sessionTimeZone,
                tlsActive,
                profile.Sha256,
                leaseCount,
                maximumConcurrentLeases,
                maximumLeaseAcquisitionMilliseconds,
                transactionCount,
                physicalOpenCount,
                0,
                closeCount,
                connectionLossObserved,
                connectionState.ToString(),
                sha);
        }
    }

    internal void ReleaseLease()
    {
        lock (sync)
        {
            Require(activeLeases == 1,
                "ARCH7B_POSTGRESQL_PINNED_SESSION_LEASE_STATE_INVALID");
            activeLeases = 0;
        }
        leaseGate.Release();
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed class Arch7bPostgreSqlPinnedSessionAuthorityLease
    : IAsyncDisposable
{
    private readonly Arch7bPostgreSqlPinnedSessionAuthority authority;
    private int disposed;

    internal Arch7bPostgreSqlPinnedSessionAuthorityLease(
        Arch7bPostgreSqlPinnedSessionAuthority authority)
    {
        this.authority = authority;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            authority.ReleaseLease();
        return ValueTask.CompletedTask;
    }
}

public sealed class Arch7bPostgreSqlPinnedSessionLease : IAsyncDisposable
{
    private readonly Arch7bPostgreSqlPinnedSession owner;
    private readonly Arch7bPostgreSqlPinnedSessionAuthorityLease authorityLease;
    private int disposed;

    internal Arch7bPostgreSqlPinnedSessionLease(
        Arch7bPostgreSqlPinnedSession owner,
        Arch7bPostgreSqlPinnedSessionAuthorityLease authorityLease,
        NpgsqlConnection connection)
    {
        this.owner = owner;
        this.authorityLease = authorityLease;
        Connection = connection;
    }

    public NpgsqlConnection Connection { get; }

    internal bool IsOwnedBy(Arch7bPostgreSqlPinnedSession session) =>
        ReferenceEquals(owner, session) &&
        Volatile.Read(ref disposed) == 0;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await authorityLease.DisposeAsync();
    }
}

public sealed class Arch7bPostgreSqlPinnedSession : IAsyncDisposable
{
    private static int processSessionCount;
    private readonly NpgsqlDataSource dataSource;
    private readonly NpgsqlConnection connection;
    private readonly string expectedUsername;
    private int openStarted;
    private int disposed;

    internal Arch7bPostgreSqlPinnedSession(
        NpgsqlDataSource dataSource,
        NpgsqlConnection connection,
        PmsShadowPostgreSqlTarget target,
        Arch7bPostgreSqlPinnedTransportProfile profile,
        string expectedUsername)
    {
        if (Interlocked.CompareExchange(
                ref processSessionCount, 1, 0) != 0)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_MULTIPLE_PINNED_SESSIONS_FORBIDDEN");
        this.dataSource = dataSource;
        this.connection = connection;
        this.expectedUsername = expectedUsername;
        Target = target;
        Profile = profile;
        Authority = new(profile);
    }

    public PmsShadowPostgreSqlTarget Target { get; }
    public Arch7bPostgreSqlPinnedTransportProfile Profile { get; }
    public Arch7bPostgreSqlPinnedSessionAuthority Authority { get; }

    public async Task<Arch7bPostgreSqlPinnedSessionEvidence> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref openStarted, 1) != 0)
            throw new InvalidOperationException(
                Arch7bPostgreSqlPinnedSessionAuthority.SecondOpen);
        var startedUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        await connection.OpenAsync(cancellationToken);
        stopwatch.Stop();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT current_database(),
                       current_user,
                       current_setting('server_version_num'),
                       current_setting('server_version'),
                       current_setting('TimeZone'),
                       clock_timestamp(),
                       COALESCE((
                           SELECT ssl FROM pg_catalog.pg_stat_ssl
                           WHERE pid = pg_catalog.pg_backend_pid()), false),
                       pg_catalog.pg_backend_pid()
                """;
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow, cancellationToken);
            Require(await reader.ReadAsync(cancellationToken),
                Arch7bPostgreSqlPinnedSessionAuthority.IdentityRowMissing);
            var database = reader.GetString(0);
            var user = reader.GetString(1);
            var versionNumber = reader.GetString(2);
            var version = reader.GetString(3);
            var timeZone = reader.GetString(4);
            var databaseUtc = reader.GetValue(5) switch
            {
                DateTime dateTime when dateTime.Kind == DateTimeKind.Utc =>
                    new DateTimeOffset(dateTime),
                DateTimeOffset dateTimeOffset
                    when dateTimeOffset.Offset == TimeSpan.Zero =>
                    dateTimeOffset,
                _ => throw new InvalidDataException(
                    "ARCH7B_POSTGRESQL_PINNED_DATABASE_TIME_NOT_UTC")
            };
            var tlsActive = reader.GetBoolean(6);
            var queryBackendProcessId = reader.GetInt32(7);
            Require(database ==
                    Arch7bBracketedGlobalFlatContract.TargetDatabase,
                Arch7bPostgreSqlPinnedSessionAuthority.DatabaseMismatch);
            Require(user == expectedUsername,
                Arch7bPostgreSqlPinnedSessionAuthority.UserMismatch);
            Require(int.Parse(versionNumber, CultureInfo.InvariantCulture) /
                    10000 == Profile.PostgreSqlMajor,
                Arch7bPostgreSqlPinnedSessionAuthority.VersionMismatch);
            Require(timeZone is "UTC" or "Etc/UTC",
                "ARCH7B_POSTGRESQL_PINNED_TIMEZONE_NOT_UTC");
            Require(databaseUtc.Offset == TimeSpan.Zero,
                "ARCH7B_POSTGRESQL_PINNED_DATABASE_TIME_NOT_UTC");
            Require(tlsActive,
                "ARCH7B_POSTGRESQL_TLS_NOT_ACTIVE");
            Require(queryBackendProcessId == connection.ProcessID,
                Arch7bPostgreSqlPinnedSessionAuthority
                    .BackendProcessIdMismatch);
            Require(Target.TargetFingerprint.Length == 64,
                "ARCH7B_POSTGRESQL_PINNED_TARGET_FINGERPRINT_INVALID");
            Authority.ObserveOpen(
                startedUtc,
                stopwatch.Elapsed,
                connection.ProcessID,
                database,
                user,
                version,
                timeZone,
                tlsActive);
            return Authority.Snapshot(connection.State);
        }
        catch
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
            throw;
        }
    }

    public async Task<Arch7bPostgreSqlPinnedSessionLease> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        var authorityLease = await Authority.AcquireAsync(
            () => connection.State, cancellationToken);
        return new(this, authorityLease, connection);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        Arch7bPostgreSqlPinnedSessionLease lease,
        PmsShadowDbContext context,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateLease(lease);
        Require(ReferenceEquals(
                context.Database.GetDbConnection(), connection),
            Arch7bPostgreSqlPinnedSessionAuthority.ContextConnectionMismatch);
        var transaction = await context.Database.BeginTransactionAsync(
            isolationLevel, cancellationToken);
        Authority.ObserveTransaction();
        return transaction;
    }

    public Exception NormalizeOperationFailure(Exception exception)
    {
        if (connection.State == ConnectionState.Open) return exception;
        Authority.ObserveConnectionLoss();
        return new InvalidDataException(
            Arch7bPostgreSqlPinnedSessionAuthority.SessionLost,
            exception);
    }

    public Arch7bPostgreSqlPinnedSessionEvidence Snapshot()
        => Authority.Snapshot(
            Volatile.Read(ref disposed) == 0
                ? connection.State
                : ConnectionState.Closed);

    internal NpgsqlConnection GetConnectionForContextFactory() =>
        connection;

    internal void ValidateLease(
        Arch7bPostgreSqlPinnedSessionLease lease)
    {
        Require(lease.IsOwnedBy(this),
            "ARCH7B_POSTGRESQL_PINNED_SESSION_LEASE_INVALID");
        Require(connection.State == ConnectionState.Open,
            Arch7bPostgreSqlPinnedSessionAuthority.SessionLost);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try
        {
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
                Authority.ObserveClose();
            }
        }
        finally
        {
            await connection.DisposeAsync();
            await dataSource.DisposeAsync();
            Interlocked.Exchange(ref processSessionCount, 0);
        }
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed class Arch7bPostgreSqlPinnedDbContextFactory
{
    private readonly Arch7bPostgreSqlPinnedSession session;
    private readonly DbContextOptions<PmsShadowDbContext> options;

    public Arch7bPostgreSqlPinnedDbContextFactory(
        Arch7bPostgreSqlPinnedSession session)
    {
        this.session = session;
        options = new DbContextOptionsBuilder<PmsShadowDbContext>()
            .UseNpgsql(
                session.GetConnectionForContextFactory(),
                contextOwnsConnection: false,
                npgsql => npgsql.SetPostgresVersion(
                    Arch7bBracketedGlobalFlatContract.PostgreSqlMajor, 0))
            .Options;
    }

    public PmsShadowDbContext CreateDbContext(
        Arch7bPostgreSqlPinnedSessionLease lease)
    {
        session.ValidateLease(lease);
        return new(options);
    }
}

public static class Arch7bPostgreSqlPinnedSessionFactory
{
    public static Arch7bPostgreSqlPinnedSession Create(
        Arch7bPostgreSqlPinnedTransportProfile profile,
        string username,
        string password,
        string applicationName,
        Arch7bPostgreSqlAccessMode accessMode,
        string rootCertificatePath)
    {
        Arch7bPostgreSqlPinnedTransportProfileContract.Validate(profile);
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
        Arch7bPostgreSqlPinnedTransportProfileContract
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
            Pooling = false,
            Multiplexing = false,
            Enlist = false,
            Timeout = profile.ConnectionTimeoutSeconds,
            CommandTimeout = profile.CommandTimeoutSeconds,
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
            var connection = dataSource.CreateConnection();
            try
            {
                return new(
                    dataSource, connection, target, profile, username);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
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
