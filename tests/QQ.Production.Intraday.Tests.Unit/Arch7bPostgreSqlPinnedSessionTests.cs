using System.Data;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlPinnedSessionTests
{
    [Fact]
    public void Pinned_profile_is_exact_and_accepted()
    {
        var profile = Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary;

        Arch7bPostgreSqlPinnedTransportProfileContract.Validate(profile);

        Assert.Equal("arch7b_postgresql_transport_profile_v2",
            profile.ContractVersion);
        Assert.Equal(
            "DIRECT_VPC_PRIMARY_HOST_VERIFY_FULL_PINNED_SESSION",
            profile.Profile);
        Assert.False(profile.Pooling);
        Assert.False(profile.Multiplexing);
        Assert.False(profile.Enlist);
        Assert.Equal(20, profile.ConnectionTimeoutSeconds);
        Assert.Equal(30, profile.CommandTimeoutSeconds);
        Assert.Equal(SslMode.VerifyFull, profile.SslMode);
        Assert.Equal(SslNegotiation.Postgres, profile.SslNegotiation);
        Assert.Equal(GssEncryptionMode.Disable, profile.GssEncryptionMode);
        Assert.Equal(64, profile.Sha256.Length);
    }

    [Fact]
    public void Pinned_profile_rejects_pooling()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                Profile() with { Pooling = true }));

        Assert.Equal(
            "ARCH7B_POSTGRESQL_PINNED_SESSION_POOLING_FORBIDDEN",
            error.Message);
    }

    [Fact]
    public void Pinned_profile_rejects_multiplexing()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                Profile() with { Multiplexing = true }));

        Assert.Equal("ARCH7B_POSTGRESQL_MULTIPLEXING_FORBIDDEN",
            error.Message);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(21)]
    public void Pinned_profile_rejects_non_twenty_second_timeout(
        int timeout)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                Profile() with { ConnectionTimeoutSeconds = timeout }));

        Assert.Equal(
            Arch7bPostgreSqlPinnedTransportProfileContract
                .ColdTimeoutMismatch,
            error.Message);
    }

    [Fact]
    public void Pinned_profile_requires_endpoint_port_and_root_ca()
    {
        Assert.Equal("ARCH7B_POSTGRESQL_DIRECT_RDS_IDENTITY_MISMATCH",
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                    Profile() with
                    {
                        RdsEndpoint = "other.example.test"
                    })).Message);
        Assert.Equal("ARCH7B_POSTGRESQL_DIRECT_PORT_MISMATCH",
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                    Profile() with { RdsPort = 15432 })).Message);
        Assert.Equal("ARCH7B_POSTGRESQL_ROOT_CA_SHA256_MISMATCH",
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
                    Profile() with
                    {
                        RootCertificateSha256 = new string('0', 64)
                    })).Message);
    }

    [Theory]
    [InlineData("--relay")]
    [InlineData("--ssm")]
    [InlineData("--session-manager-plugin")]
    [InlineData("--portproxy")]
    public void Pinned_profile_rejects_fallback_transport(string argument)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlPinnedTransportProfileContract
                .ValidateCommandLine(
                    new[] { argument },
                    Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint,
                    5432));

        Assert.Equal("ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN",
            error.Message);
    }

    [Fact]
    public void Authority_accepts_exactly_one_physical_open()
    {
        var authority = Authority();
        Open(authority, 4242);

        var error = Assert.Throws<InvalidDataException>(() =>
            Open(authority, 4242));

        Assert.Equal(
            Arch7bPostgreSqlPinnedSessionAuthority.SecondOpen,
            error.Message);
        var evidence = authority.Snapshot(ConnectionState.Open);
        Assert.Equal(1, evidence.PhysicalOpenCount);
        Assert.Equal(0, evidence.PhysicalReconnectCount);
        Assert.Equal(4242, evidence.BackendProcessId);
    }

    [Fact]
    public void Authority_rejects_invalid_backend_pid()
    {
        var authority = Authority();

        var error = Assert.Throws<InvalidDataException>(() =>
            Open(authority, 0));

        Assert.Equal("ARCH7B_POSTGRESQL_BACKEND_PROCESS_ID_INVALID",
            error.Message);
    }

    [Fact]
    public async Task Sequential_leases_are_accepted_and_counted()
    {
        var authority = Authority();
        Open(authority, 4242);

        await using (await authority.AcquireAsync(
                         () => ConnectionState.Open))
        {
            authority.ObserveTransaction();
        }
        await using (await authority.AcquireAsync(
                         () => ConnectionState.Open))
        {
        }

        var evidence = authority.Snapshot(ConnectionState.Open);
        Assert.Equal(2, evidence.SessionLeaseCount);
        Assert.Equal(1, evidence.MaximumConcurrentLeases);
        Assert.Equal(1, evidence.TransactionCount);
        Assert.InRange(evidence.MaximumLeaseAcquisitionMilliseconds,
            0, 100);
    }

    [Fact]
    public async Task Concurrent_lease_is_rejected_at_local_slo()
    {
        var authority = Authority();
        Open(authority, 4242);
        await using var first = await authority.AcquireAsync(
            () => ConnectionState.Open);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            authority.AcquireAsync(() => ConnectionState.Open));

        Assert.Equal(
            Arch7bPostgreSqlPinnedSessionAuthority.LeaseContention,
            error.Message);
    }

    [Fact]
    public async Task Lease_dispose_releases_local_lock_after_exception()
    {
        var authority = Authority();
        Open(authority, 4242);
        try
        {
            await using var lease = await authority.AcquireAsync(
                () => ConnectionState.Open);
            throw new InvalidOperationException("fixture");
        }
        catch (InvalidOperationException)
        {
        }

        await using var next = await authority.AcquireAsync(
            () => ConnectionState.Open);
        Assert.Equal(2,
            authority.Snapshot(ConnectionState.Open).SessionLeaseCount);
    }

    [Fact]
    public async Task Closed_connection_is_classified_as_lost_without_reopen()
    {
        var authority = Authority();
        Open(authority, 4242);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            authority.AcquireAsync(() => ConnectionState.Closed));

        Assert.Equal(
            Arch7bPostgreSqlPinnedSessionAuthority.SessionLost,
            error.Message);
        var evidence = authority.Snapshot(ConnectionState.Closed);
        Assert.True(evidence.ConnectionLossObserved);
        Assert.Equal(1, evidence.PhysicalOpenCount);
        Assert.Equal(0, evidence.PhysicalReconnectCount);
    }

    [Fact]
    public void Close_is_observed_exactly_once()
    {
        var authority = Authority();
        Open(authority, 4242);
        authority.ObserveClose();

        var evidence = authority.Snapshot(ConnectionState.Closed);
        Assert.Equal(1, evidence.CloseCount);
        Assert.Equal("Closed", evidence.ConnectionState);
        Assert.Equal(64, evidence.EvidenceSha256.Length);
        Assert.Throws<InvalidDataException>(authority.ObserveClose);
    }

    [Fact]
    public void Selected_store_and_reader_never_open_or_close_connection()
    {
        var root = RepoRoot();
        var store = File.ReadAllText(Path.Combine(root, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bAppendOnlyGlobalFlatPositionImport.cs"));
        var reader = File.ReadAllText(Path.Combine(root, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bBracketedGlobalFlatPositionSnapshot.cs"));

        Assert.DoesNotContain("OpenConnectionAsync", store,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CloseConnectionAsync", store,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ObserveLogicalCheckout", store,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompleteLogicalCheckout", store,
            StringComparison.Ordinal);
        var selectedReader = reader[
            reader.IndexOf("public sealed class Arch7bRequiredPmsUniverseReader",
                StringComparison.Ordinal)..];
        Assert.DoesNotContain("OpenAsync(", selectedReader,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CloseAsync(", selectedReader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Pinned_runtime_has_one_physical_open_and_one_connection()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bPostgreSqlPinnedSession.cs"));

        Assert.Equal(1, Count(source, "await connection.OpenAsync("));
        Assert.Equal(1, Count(source, "dataSource.CreateConnection()"));
        Assert.Equal(1, Count(source,
            "new NpgsqlDataSourceBuilder("));
        Assert.Contains("Pooling = false", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MinPoolSize", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MaxPoolSize", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionIdleLifetime", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionLifetime", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ef_factory_uses_existing_non_owned_connection()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bPostgreSqlPinnedSession.cs"));

        Assert.Contains("contextOwnsConnection: false", source,
            StringComparison.Ordinal);
        Assert.Contains("CreateDbContext(", source,
            StringComparison.Ordinal);
        Assert.Contains(
            Arch7bPostgreSqlPinnedSessionAuthority.ContextConnectionMismatch, source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Fast_paths_open_once_in_parallel_with_core_validation()
    {
        var root = RepoRoot();
        foreach (var path in new[]
                 {
                     Path.Combine(root, "tools",
                         "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
                         "Program.cs"),
                     Path.Combine(root, "tools",
                         "QQ.Production.Intraday.Tools.Arch7bGlobalFlatPositionSnapshot",
                         "Program.cs")
                 })
        {
            var source = File.ReadAllText(path);
            Assert.Contains(
                "var supervisor = new Arch7bPostgreSqlPinnedOpenSupervisor(",
                source, StringComparison.Ordinal);
            Assert.Contains("var openTask = supervisor.StartOpen();",
                source, StringComparison.Ordinal);
            Assert.Contains("var coreTask = Task.Run",
                source, StringComparison.Ordinal);
            Assert.Contains("supervisor.WaitForOpenAndPeerAsync(coreTask)",
                source, StringComparison.Ordinal);
            Assert.DoesNotContain("await using var runtime", source,
                StringComparison.Ordinal);
            Assert.DoesNotContain("WarmAsync", source,
                StringComparison.Ordinal);
            Assert.DoesNotContain("runtime.DataSource", source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Qualification_mode_is_read_only_and_historical()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
            "Program.cs"));

        Assert.Contains("\"qualify-pinned-postgresql-session\"",
            source, StringComparison.Ordinal);
        Assert.Contains("for (var index = 0; index < 10; index++)",
            source, StringComparison.Ordinal);
        Assert.Contains("historicalFixture: true",
            source, StringComparison.Ordinal);
        Assert.Contains(
            "ARCH7B_PINNED_POSTGRESQL_SESSION_QUALIFIED",
            source, StringComparison.Ordinal);
        Assert.Contains("no_database_write = true",
            source, StringComparison.Ordinal);
    }

    private static Arch7bPostgreSqlPinnedTransportProfile Profile() =>
        Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary;

    private static Arch7bPostgreSqlPinnedSessionAuthority Authority() =>
        new(Profile());

    private static void Open(
        Arch7bPostgreSqlPinnedSessionAuthority authority,
        int processId)
    {
        authority.ObserveOpen(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromSeconds(15),
            processId,
            Arch7bBracketedGlobalFlatContract.TargetDatabase,
            "qq_arch7b_position_importer",
            "18.4",
            "UTC",
            true);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ??
               throw new DirectoryNotFoundException();
    }
}
