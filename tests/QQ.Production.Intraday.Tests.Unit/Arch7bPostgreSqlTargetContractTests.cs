using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlTargetContractTests
{
    private const string TestDatabase = "qq_pms_shadow_arch6d_test";
    private const string TestProfile = "ARCH7B_LOCAL_TEST";

    [Fact]
    public void Explicitly_allowed_loopback_test_target_is_valid()
    {
        var target = Validate(Builder("127.0.0.1"), Settings(allowLoopback: true));

        Assert.Equal(PmsShadowPostgreSqlTargetContract.LoopbackKind, target.TargetKind);
        Assert.Equal(TestDatabase, target.Database);
        Assert.Equal(64, target.TargetFingerprint.Length);
        Assert.Equal(18, target.ExpectedPostgresMajor);
    }

    [Fact]
    public void Loopback_is_rejected_when_not_explicitly_allowed()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(Builder("localhost"), Settings(allowLoopback: false)));

        Assert.Equal("POSTGRESQL_TARGET_LOOPBACK_NOT_ALLOWED", error.Message);
    }

    [Fact]
    public void Remote_verify_full_test_target_is_valid_without_connecting()
    {
        var builder = Builder("arch7b-test.cluster-example.eu-west-1.rds.amazonaws.com");
        builder.SslMode = SslMode.VerifyFull;

        var target = Validate(builder, Settings(requireTls: true, allowLoopback: false));

        Assert.Equal(PmsShadowPostgreSqlTargetContract.RemoteTlsKind, target.TargetKind);
        Assert.Equal("VERIFYFULL", target.TlsPolicy);
    }

    [Fact]
    public void Remote_target_without_tls_fails_closed()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(Builder("db.example.test"), Settings(allowLoopback: false)));

        Assert.Equal("POSTGRESQL_TARGET_REMOTE_TLS_REQUIRED", error.Message);
    }

    [Fact]
    public void Remote_target_with_trusted_server_certificate_fails_closed()
    {
        var builder = Builder("db.example.test");
        builder.SslMode = SslMode.VerifyFull;
#pragma warning disable CS0618 // Exercises rejection of the legacy bypass keyword.
        builder.TrustServerCertificate = true;
#pragma warning restore CS0618

        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(builder, Settings(requireTls: true, allowLoopback: false)));

        Assert.Equal("POSTGRESQL_TARGET_REMOTE_TRUST_SERVER_CERTIFICATE_FORBIDDEN",
            error.Message);
    }

    [Fact]
    public void Database_must_match_expected_database_exactly()
    {
        var builder = Builder("127.0.0.1");
        builder.Database = "other_test_database";

        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(builder, Settings()));

        Assert.Equal("POSTGRESQL_TARGET_DATABASE_MISMATCH", error.Message);
    }

    [Fact]
    public void Expected_database_is_required_before_connectivity()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(Builder("127.0.0.1"), Settings() with { ExpectedDatabase = "" }));

        Assert.Equal("POSTGRESQL_TARGET_EXPECTED_DATABASE_REQUIRED", error.Message);
    }

    [Fact]
    public void Non_test_environment_fails_closed()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(Builder("127.0.0.1"), Settings() with
            {
                ExpectedEnvironment = "PRODUCTION"
            }));

        Assert.Equal("POSTGRESQL_TARGET_ENVIRONMENT_NOT_TEST", error.Message);
    }

    [Fact]
    public void Fingerprint_is_normalized_stable_distinct_and_contains_no_credentials()
    {
        var left = Validate(RemoteBuilder("DB.EXAMPLE.TEST."), Settings(
            requireTls: true, allowLoopback: false));
        var normalized = Validate(RemoteBuilder("db.example.test"), Settings(
            requireTls: true, allowLoopback: false));
        var different = Validate(RemoteBuilder("other.example.test"), Settings(
            requireTls: true, allowLoopback: false));
        var material = PmsShadowPostgreSqlTargetContract.FingerprintMaterial(
            left.Host, left.Port, left.Database, left.ExpectedEnvironment, left.ExpectedSchema,
            left.ExpectedPostgresMajor, left.TargetProfileId, left.TargetKind, left.TlsPolicy);

        Assert.Equal(left.TargetFingerprint, normalized.TargetFingerprint);
        Assert.NotEqual(left.TargetFingerprint, different.TargetFingerprint);
        Assert.DoesNotContain("arch7b_test_principal", material, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 32), material, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", material, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marker_bound_to_another_target_fails_closed()
    {
        var close = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        var options = PmsShadowFreshSlotHandoffOptions.Create(
            Path.GetTempPath(), PmsShadowIntradayCadenceContract.WindowEnding(close),
            "source", "run", new string('c', 40), TestProfile, new string('a', 64));
        var marker = new PmsShadowFreshSlotReadyMarker(
            PmsShadowFreshSlotHandoffContract.Version, options.SlotId, close, options.SourceSessionId,
            "artifact.jsonl", new string('d', 64), new string('e', 64),
            new string('1', 64), new string('2', 64), close, 1,
            options.RepositoryCommit, options.TargetProfileId, new string('b', 64),
            PmsShadowFreshSlotHandoffContract.Environment, true);

        var error = Assert.Throws<InvalidDataException>(() =>
            PmsShadowFreshSlotReadyMarkerStore.Validate(options, marker, false));

        Assert.Equal("HANDOFF_READY_MARKER_TARGET_MISMATCH", error.Message);
    }

    [Fact]
    public void Operational_cli_has_one_portable_target_and_no_local_version_pin()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch6fEconomicReplay",
            "Arch7bPrearmedFreshSlotHandoffCli.cs"));

        Assert.DoesNotContain("qq_pms_shadow_arch6d_test", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetPostgresVersion", source, StringComparison.Ordinal);
        Assert.Contains("--expected-postgres-major", source, StringComparison.Ordinal);
        Assert.Contains(
            "serverVersionNumber / 10000 == configuredTarget.ExpectedPostgresMajor",
            source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"localhost\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"127.0.0.1\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"::1\"", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "new HandoffContextFactory"));
        Assert.Equal(1, Count(source, "UseNpgsql("));
        Assert.DoesNotContain("Migrate", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Target_contract_requires_credentials_but_observability_is_non_secret()
    {
        var missingCredential = Builder("127.0.0.1");
        missingCredential.Remove("Password");
        var error = Assert.Throws<InvalidDataException>(() =>
            Validate(missingCredential, Settings()));
        var target = Validate(Builder("127.0.0.1"), Settings());

        Assert.Equal("POSTGRESQL_TARGET_CREDENTIAL_REQUIRED", error.Message);
        Assert.DoesNotContain("principal", target.ObservableIdentity,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", target.ObservableIdentity,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PmsShadowPostgreSqlTarget Validate(
        NpgsqlConnectionStringBuilder builder,
        PmsShadowPostgreSqlTargetSettings settings) =>
        PmsShadowPostgreSqlTargetContract.Validate(builder.ConnectionString, settings);

    private static NpgsqlConnectionStringBuilder Builder(string host) => new()
    {
        Host = host,
        Port = 5432,
        Database = TestDatabase,
        Username = "arch7b_test_principal",
        Password = new string('x', 32),
        SslMode = SslMode.Disable
    };

    private static NpgsqlConnectionStringBuilder RemoteBuilder(string host)
    {
        var builder = Builder(host);
        builder.SslMode = SslMode.VerifyFull;
        return builder;
    }

    private static PmsShadowPostgreSqlTargetSettings Settings(
        bool requireTls = false,
        bool allowLoopback = true) =>
        new(PmsShadowPostgreSqlTargetContract.TestEnvironment, TestDatabase,
            PmsShadowStateContract.SchemaName, 18, requireTls, allowLoopback, TestProfile);

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
