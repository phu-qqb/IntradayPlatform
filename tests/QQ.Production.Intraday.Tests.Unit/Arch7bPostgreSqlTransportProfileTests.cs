using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlTransportProfileTests
{
    [Fact]
    public void Direct_primary_profile_is_exact_and_accepted()
    {
        var profile = Arch7bPostgreSqlTransportProfile.DirectPrimary;

        Arch7bPostgreSqlTransportProfileContract.Validate(profile);

        Assert.Equal(
            "arch7b_postgresql_transport_profile_v1",
            profile.ContractVersion);
        Assert.Equal("DIRECT_VPC_PRIMARY_HOST_VERIFY_FULL",
            profile.Profile);
        Assert.Equal(
            "db-arch7b-pms-shadow-test.cx0goossu17s.eu-west-2.rds.amazonaws.com",
            profile.RdsEndpoint);
        Assert.Equal(5432, profile.RdsPort);
        Assert.Equal(SslMode.VerifyFull, profile.SslMode);
        Assert.Equal(SslNegotiation.Postgres, profile.SslNegotiation);
        Assert.Equal(GssEncryptionMode.Disable, profile.GssEncryptionMode);
        Assert.Equal("CHAIN_AND_HOSTNAME",
            profile.CertificateValidationMode);
        Assert.Equal(64, profile.Sha256.Length);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(21)]
    public void Cold_timeout_other_than_twenty_is_rejected(int timeout)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlTransportProfileContract.Validate(
                Arch7bPostgreSqlTransportProfile.DirectPrimary with
                {
                    ConnectionTimeoutSeconds = timeout
                }));

        Assert.Equal(
            Arch7bPostgreSqlTransportProfileContract.ColdTimeoutMismatch,
            error.Message);
    }

    [Fact]
    public void Direct_endpoint_and_port_are_required()
    {
        Assert.Equal("ARCH7B_POSTGRESQL_DIRECT_RDS_IDENTITY_MISMATCH",
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPostgreSqlTransportProfileContract.Validate(
                    Arch7bPostgreSqlTransportProfile.DirectPrimary with
                    {
                        RdsEndpoint = "other.example.test"
                    })).Message);
        Assert.Equal("ARCH7B_POSTGRESQL_DIRECT_PORT_MISMATCH",
            Assert.Throws<InvalidDataException>(() =>
                Arch7bPostgreSqlTransportProfileContract.Validate(
                    Arch7bPostgreSqlTransportProfile.DirectPrimary with
                    {
                        RdsPort = 15432
                    })).Message);
    }

    [Fact]
    public void Loopback_profile_is_rejected()
    {
        var profile = Arch7bPostgreSqlTransportProfile.DirectPrimary with
        {
            RdsEndpoint = "127.0.0.1",
            RdsInstanceId = "db-arch7b-pms-shadow-test"
        };

        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlTransportProfileContract.Validate(profile));

        Assert.Equal("ARCH7B_POSTGRESQL_LOOPBACK_FORBIDDEN",
            error.Message);
    }

    [Theory]
    [InlineData("--connect-host",
        "ARCH7B_POSTGRESQL_CONNECT_HOST_OVERRIDE_FORBIDDEN")]
    [InlineData("--connect-port",
        "ARCH7B_POSTGRESQL_CONNECT_PORT_OVERRIDE_FORBIDDEN")]
    [InlineData("--target-host",
        "ARCH7B_POSTGRESQL_TARGET_HOST_OVERRIDE_FORBIDDEN")]
    [InlineData("--relay",
        "ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN")]
    [InlineData("--ssm",
        "ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN")]
    [InlineData("--session-manager-plugin",
        "ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN")]
    [InlineData("--portproxy",
        "ARCH7B_POSTGRESQL_FALLBACK_TRANSPORT_FORBIDDEN")]
    public void Forbidden_transport_override_is_rejected(
        string argument,
        string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlTransportProfileContract.ValidateCommandLine(
                new[] { argument },
                Arch7bPostgreSqlTransportProfile.DirectEndpoint,
                5432));

        Assert.Equal(expected, error.Message);
    }

    [Fact]
    public void Exact_root_ca_sha_is_required()
    {
        Arch7bPostgreSqlTransportProfileContract
            .ValidateRootCertificateSha256(
                Arch7bPostgreSqlTransportProfile
                    .ExpectedRootCertificateSha256);

        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7bPostgreSqlTransportProfileContract
                .ValidateRootCertificateSha256(new string('0', 64)));
        Assert.Equal("ARCH7B_POSTGRESQL_ROOT_CA_SHA256_MISMATCH",
            error.Message);
    }

    [Fact]
    public void Pool_profile_is_exactly_one_by_one()
    {
        var profile = Arch7bPostgreSqlTransportProfile.DirectPrimary;

        Assert.True(profile.Pooling);
        Assert.Equal(1, profile.MinimumPoolSize);
        Assert.Equal(1, profile.MaximumPoolSize);
        Assert.False(profile.Multiplexing);
        Assert.False(profile.NoResetOnClose);
        Assert.False(profile.Enlist);
        Assert.False(profile.IncludeErrorDetail);
        Assert.False(profile.LogParameters);
        Assert.False(profile.PersistSecurityInfo);
    }

    [Fact]
    public void Warm_backend_pid_is_reused_across_logical_checkouts()
    {
        var authority = Authority();
        Warm(authority, 4242);
        authority.EnterPostP2CriticalPath();

        authority.ObserveLogicalCheckout(
            4242, TimeSpan.FromMilliseconds(4));
        authority.CompleteLogicalCheckout();
        authority.ObserveLogicalCheckout(
            4242, TimeSpan.FromMilliseconds(3));
        authority.CompleteLogicalCheckout();

        var evidence = authority.Snapshot();
        Assert.Equal(1, evidence.ColdPhysicalOpenCount);
        Assert.Equal(0, evidence.PhysicalReconnectCount);
        Assert.Equal(3, evidence.LogicalCheckoutCount);
        Assert.Equal(1, evidence.MaximumConcurrentLogicalConnections);
        Assert.True(evidence.WarmedBeforeCriticalDatabaseWork);
        Assert.Equal(64, evidence.EvidenceSha256.Length);
    }

    [Fact]
    public void Changed_backend_pid_after_p2_is_rejected()
    {
        var authority = Authority();
        Warm(authority, 4242);
        authority.EnterPostP2CriticalPath();

        var error = Assert.Throws<InvalidDataException>(() =>
            authority.ObserveLogicalCheckout(
                4343, TimeSpan.FromMilliseconds(2)));

        Assert.Equal(
            Arch7bPostgreSqlWarmConnectionAuthority.PhysicalReconnect,
            error.Message);
    }

    [Fact]
    public void Concurrent_logical_checkout_is_rejected()
    {
        var authority = Authority();
        authority.ObserveColdOpen(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMilliseconds(1),
            TimeSpan.FromMilliseconds(1),
            4242);

        var error = Assert.Throws<InvalidDataException>(() =>
            authority.ObserveLogicalCheckout(
                4242, TimeSpan.FromMilliseconds(1)));

        Assert.Equal(
            Arch7bPostgreSqlWarmConnectionAuthority.ConcurrentCheckout,
            error.Message);
    }

    [Fact]
    public void Pooled_checkout_over_one_second_is_rejected()
    {
        var authority = Authority();
        Warm(authority, 4242);

        var error = Assert.Throws<InvalidDataException>(() =>
            authority.ObserveLogicalCheckout(
                4242, TimeSpan.FromMilliseconds(1001)));

        Assert.Equal(
            Arch7bPostgreSqlWarmConnectionAuthority.SlowPooledCheckout,
            error.Message);
    }

    [Fact]
    public void Consumers_select_pinned_factory_and_no_local_transport_builder()
    {
        var root = RepoRoot();
        var consumer = File.ReadAllText(Path.Combine(root, "tools",
            "QQ.Production.Intraday.Tools.Arch7bGlobalFlatPositionSnapshot",
            "Program.cs"));
        var importer = File.ReadAllText(Path.Combine(root, "tools",
            "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
            "Program.cs"));

        Assert.Contains("Arch7bPostgreSqlPinnedSessionFactory.Create",
            consumer, StringComparison.Ordinal);
        Assert.Contains("Arch7bPostgreSqlPinnedSessionFactory.Create",
            importer, StringComparison.Ordinal);
        Assert.DoesNotContain("Arch7bPostgreSqlDataSourceFactory.Create",
            consumer, StringComparison.Ordinal);
        Assert.DoesNotContain("Arch7bPostgreSqlDataSourceFactory.Create",
            importer, StringComparison.Ordinal);
        Assert.DoesNotContain("new NpgsqlConnectionStringBuilder",
            consumer, StringComparison.Ordinal);
        Assert.DoesNotContain("new NpgsqlConnectionStringBuilder",
            importer, StringComparison.Ordinal);
        Assert.DoesNotContain("UseSslClientAuthenticationOptionsCallback",
            consumer, StringComparison.Ordinal);
        Assert.DoesNotContain("UseSslClientAuthenticationOptionsCallback",
            importer, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildLogicalConnectionString",
            consumer, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildLogicalConnectionString",
            importer, StringComparison.Ordinal);
        Assert.Contains(
            "ARCH7B_POSTGRESQL_MULTIPLE_PINNED_SESSIONS_FORBIDDEN",
            File.ReadAllText(Path.Combine(RepoRoot(), "src",
                "QQ.Production.Intraday.Infrastructure.PostgreSql",
                "Arch7bPostgreSqlPinnedSession.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Fast_wrapper_opens_pinned_session_in_parallel()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
            "Program.cs"));

        Assert.Contains("var openTask = supervisor.StartOpen();",
            source, StringComparison.Ordinal);
        Assert.Contains("var coreTask = Task.Run",
            source, StringComparison.Ordinal);
        Assert.Contains(
            "await supervisor.WaitForOpenAndPeerAsync(coreTask)",
            source, StringComparison.Ordinal);
        Assert.DoesNotContain("await using var runtime",
            source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnterPostP2CriticalPath",
            source, StringComparison.Ordinal);
        Assert.Contains(
            "new Arch7bPositionImportStore(\n    contextFactory, target, runtime)",
            source.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void Store_and_universe_reader_use_pinned_leases()
    {
        var root = RepoRoot();
        var store = File.ReadAllText(Path.Combine(root, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bAppendOnlyGlobalFlatPositionImport.cs"));
        var reader = File.ReadAllText(Path.Combine(root, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bBracketedGlobalFlatPositionSnapshot.cs"));

        Assert.Contains("VerifyPostCommitAsync", store,
            StringComparison.Ordinal);
        Assert.Contains("session.AcquireAsync", store,
            StringComparison.Ordinal);
        Assert.Contains("session.AcquireAsync", reader,
            StringComparison.Ordinal);
        Assert.DoesNotContain("OpenConnectionAsync", store,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CloseConnectionAsync", store,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ObserveLogicalCheckout", reader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Economic_freshness_contract_remains_three_hundred_seconds()
    {
        Assert.Equal(300,
            Arch7bPositionImportContract.MaximumAgeSeconds);
    }

    private static Arch7bPostgreSqlWarmConnectionAuthority Authority() =>
        new(Arch7bPostgreSqlTransportProfile.DirectPrimary);

    private static void Warm(
        Arch7bPostgreSqlWarmConnectionAuthority authority,
        int processId)
    {
        var started = new DateTimeOffset(
            2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        authority.ObserveColdOpen(
            started,
            started.AddSeconds(15),
            TimeSpan.FromSeconds(15),
            processId);
        authority.CompleteColdLogicalCheckout();
    }

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
