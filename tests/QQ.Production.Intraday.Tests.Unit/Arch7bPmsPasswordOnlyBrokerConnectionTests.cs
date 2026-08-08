using System.Security.Cryptography;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPmsPasswordOnlyBrokerConnectionTests
{
    [Fact]
    public void Password_only_environment_and_sealed_target_are_accepted_without_connecting()
    {
        var settings = Settings(out var rootCa);
        try
        {
            using var value = Arch7bPmsPasswordOnlyBrokerConnection.Create(settings,
                Environment(new Dictionary<string, string>
                {
                    [Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable] =
                        "offline-password-value"
                }));

            Assert.Equal(settings.ExpectedTargetFingerprint, value.Target.TargetFingerprint);
            Assert.Equal(PmsShadowPostgreSqlTargetContract.RemoteTlsKind, value.Target.TargetKind);
            Assert.Contains("SSL Mode=VerifyFull", value.ConnectionString, StringComparison.Ordinal);
            Assert.DoesNotContain("offline-password-value", value.Target.ObservableIdentity,
                StringComparison.Ordinal);
        }
        finally { File.Delete(rootCa); }
    }

    [Fact]
    public void Legacy_full_connection_string_is_rejected_in_broker_mode()
    {
        AssertBlocker("ARCH7B_PMS_BROKER_FULL_CONNECTION_STRING_FORBIDDEN", values =>
            values[Arch7bPmsPasswordOnlyBrokerConnection.LegacyConnectionEnvironmentVariable] =
                "Host=forbidden;Password=forbidden");
    }

    [Fact]
    public void Missing_password_is_rejected()
    {
        AssertBlocker("ARCH7B_PMS_BROKER_PASSWORD_ENV_REQUIRED", values =>
            values.Remove(Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable));
    }

    [Fact]
    public void Connection_string_in_password_variable_is_rejected()
    {
        AssertBlocker("ARCH7B_PMS_BROKER_PASSWORD_ONLY_REQUIRED", values =>
            values[Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable] =
                "Host=forbidden;Password=forbidden");
    }

    [Fact]
    public void Host_and_database_mismatches_are_rejected()
    {
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_TARGET_HOST_MISMATCH",
            value => value with { Host = "other.invalid" });
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_TARGET_DATABASE_MISMATCH",
            value => value with { Database = "other_test" });
    }

    [Fact]
    public void Tls_and_connection_policy_mismatches_are_rejected()
    {
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_TARGET_TLS_MISMATCH",
            value => value with { RequireTls = false });
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_CONNECTION_POLICY_MISMATCH",
            value => value with { Pooling = true });
    }

    [Fact]
    public void Root_ca_and_target_fingerprint_mismatches_are_rejected()
    {
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_ROOT_CA_MISMATCH",
            value => value with { ExpectedRootCertificateSha256 = new string('0', 64) });
        AssertSettingsBlocker("ARCH7B_PMS_BROKER_TARGET_FINGERPRINT_MISMATCH",
            value => value with { ExpectedTargetFingerprint = new string('0', 64) });
    }

    [Fact]
    public void Contract_names_exactly_one_password_variable_and_no_secret_target_field()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bPmsPasswordOnlyBrokerConnection.cs"));

        Assert.Equal("arch7b_pms_password_only_broker_connection_contract_v1",
            Arch7bPmsPasswordOnlyBrokerConnection.ContractVersion);
        Assert.Equal("QQ_ARCH7B_POSITION_IMPORT_FAST_PATH",
            Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable);
        Assert.Contains("FULL_CONNECTION_STRING_FORBIDDEN", source, StringComparison.Ordinal);
        Assert.DoesNotContain("password from argument", source, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertBlocker(string expected, Action<Dictionary<string, string>> mutate)
    {
        var settings = Settings(out var rootCa);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable] = "offline-password"
        };
        mutate(values);
        try
        {
            var error = Assert.Throws<InvalidDataException>(() =>
                Arch7bPmsPasswordOnlyBrokerConnection.Create(settings, Environment(values)));
            Assert.Equal(expected, error.Message);
        }
        finally { File.Delete(rootCa); }
    }

    private static void AssertSettingsBlocker(string expected,
        Func<Arch7bPmsPasswordOnlyBrokerConnectionSettings,
            Arch7bPmsPasswordOnlyBrokerConnectionSettings> mutate)
    {
        var settings = Settings(out var rootCa);
        try
        {
            var error = Assert.Throws<InvalidDataException>(() =>
                Arch7bPmsPasswordOnlyBrokerConnection.Create(mutate(settings),
                    Environment(new Dictionary<string, string>
                    {
                        [Arch7bPmsPasswordOnlyBrokerConnection.PasswordEnvironmentVariable] =
                            "offline-password"
                    })));
            Assert.Equal(expected, error.Message);
        }
        finally { File.Delete(rootCa); }
    }

    private static Arch7bPmsPasswordOnlyBrokerConnectionSettings Settings(out string rootCa)
    {
        rootCa = Path.Combine(Path.GetTempPath(), "arch7b-root-" + Guid.NewGuid().ToString("N") + ".pem");
        File.WriteAllText(rootCa, "offline-root-authority-fixture");
        var rootSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(rootCa)));
        var fingerprint = PmsShadowPostgreSqlTargetContract.Fingerprint(
            Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint, 5432,
            "qq_pms_shadow_arch7b_test", "TEST", PmsShadowStateContract.SchemaName, 18,
            "ARCH7B_RDS_TEST", PmsShadowPostgreSqlTargetContract.RemoteTlsKind, "VERIFYFULL");
        return new(Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint, 5432,
            "qq_pms_shadow_arch7b_test", "qq_arch7b_position_importer", "TEST",
            PmsShadowStateContract.SchemaName, 18, "ARCH7B_RDS_TEST", fingerprint,
            rootCa, rootSha, true, false, false, false, false);
    }

    private static Func<string, string?> Environment(IReadOnlyDictionary<string, string> values) =>
        name => values.GetValueOrDefault(name);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
