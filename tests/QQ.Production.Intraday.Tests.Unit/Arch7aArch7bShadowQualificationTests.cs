using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.Arch7aShadowQualification;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7aArch7bShadowQualificationTests
{
    [Fact]
    public void Qualify_shadow_command_contract_is_accepted_without_secret_or_database()
    {
        var parsed = Arch7aArch7bShadowQualificationArguments.Parse(ValidArguments());

        Assert.Equal("qualify-shadow", parsed.Mode);
        Assert.True(parsed.ValidateCommandContractOnly);
        Assert.Equal("ARCH7B_RDS_TEST", parsed.TargetProfileId);
        Assert.Equal("qq_pms_shadow_arch7b_test", parsed.ExpectedDatabase);
        Assert.Equal(18, parsed.ExpectedPostgresMajor);
        Assert.False(parsed.AllowLoopback);
        Assert.True(parsed.NoOrder);
    }

    [Fact]
    public void Command_manifest_v2_roundtrips_through_the_real_parser()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", "architecture", "arch7b",
            "arch7b-position-market-live-command-manifest.json")));
        Assert.Equal("arch7b_position_market_live_command_manifest_v2",
            document.RootElement.GetProperty("contract").GetString());
        var command = document.RootElement.GetProperty("commands").EnumerateArray()
            .Single(value => value.GetProperty("owner").GetString() ==
                             "arch7a-qualification");
        var arguments = new List<string>();
        foreach (var property in command.GetProperty("arguments").EnumerateObject())
        {
            arguments.Add(property.Name);
            arguments.Add(ResolveManifestValue(property.Name,
                property.Value.GetString()!));
        }
        var parsed = Arch7aArch7bShadowQualificationArguments.Parse(arguments);
        Assert.True(parsed.ValidateCommandContractOnly);
        Assert.Equal(Arch7aArch7bShadowQualificationArguments.TargetFingerprint,
            parsed.ExpectedTargetFingerprint);
    }

    [Fact]
    public void Historical_manifest_v1_is_not_selected()
    {
        var manifest = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", "architecture", "arch7b",
            "arch7b-position-market-live-command-manifest.json"));
        Assert.DoesNotContain(
            "\"contract\": \"arch7b_position_market_live_command_manifest_v1\"",
            manifest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--expected-database", "qq_pms_shadow_arch6d_test",
        "ARCH7A_ARCH7B_TARGET_DATABASE_MISMATCH")]
    [InlineData("--expected-postgres-major", "16",
        "ARCH7A_ARCH7B_POSTGRESQL_MAJOR_MISMATCH")]
    [InlineData("--allow-loopback", "true",
        "ARCH7A_ARCH7B_LOOPBACK_FORBIDDEN")]
    [InlineData("--root-certificate", @"D:\QQFund\ARCH7B\global-bundle.pem",
        "ARCH7A_ARCH7B_ROOT_CA_PATH_MISMATCH")]
    [InlineData("--expected-root-certificate-sha256",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "ARCH7A_ARCH7B_ROOT_CA_SHA256_MISMATCH")]
    [InlineData("--expected-target-fingerprint",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "ARCH7A_ARCH7B_TARGET_FINGERPRINT_MISMATCH")]
    public void Stale_or_weakened_target_contract_is_rejected(
        string name, string value, string expected)
    {
        var arguments = ValidArguments();
        arguments[arguments.IndexOf(name) + 1] = value;

        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bShadowQualificationArguments.Parse(arguments));

        Assert.Equal(expected, error.Message);
    }

    [Theory]
    [InlineData("--economic-revision-id",
        "ARCH7A_ARCH7B_ARGUMENT_REQUIRED:--economic-revision-id")]
    [InlineData("--slot-id", "ARCH7A_ARCH7B_ARGUMENT_REQUIRED:--slot-id")]
    [InlineData("--position-market-revision-binding-path",
        "ARCH7A_ARCH7B_ARGUMENT_REQUIRED:--position-market-revision-binding-path")]
    [InlineData("--expected-position-market-revision-binding-sha256",
        "ARCH7A_ARCH7B_ARGUMENT_REQUIRED:--expected-position-market-revision-binding-sha256")]
    [InlineData("--no-order", "ARCH7A_ARCH7B_ARGUMENT_REQUIRED:--no-order")]
    public void Required_arguments_are_fail_closed(string name, string expected)
    {
        var arguments = ValidArguments();
        var index = arguments.IndexOf(name);
        arguments.RemoveRange(index, 2);

        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bShadowQualificationArguments.Parse(arguments));

        Assert.Equal(expected, error.Message);
    }

    [Fact]
    public void Duplicate_argument_is_rejected()
    {
        var arguments = ValidArguments();
        arguments.Add("--slot-id");
        arguments.Add("other");

        var error = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bShadowQualificationArguments.Parse(arguments));

        Assert.Equal("ARCH7A_ARCH7B_DUPLICATE_ARGUMENT:--slot-id", error.Message);
    }

    [Fact]
    public void Exact_eight_migration_contract_is_required()
    {
        Arch7aArch7bMigrationContract.RequireExact(
            PmsShadowStateContract.MigrationIds, false);
        Assert.Equal(8, PmsShadowStateContract.MigrationIds.Count);

        var seven = PmsShadowStateContract.MigrationIds.Take(7).ToArray();
        var missing = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bMigrationContract.RequireExact(seven, false));
        Assert.Equal(Arch7aArch7bMigrationContract.ExactSetMismatch,
            missing.Message);

        var extra = PmsShadowStateContract.MigrationIds
            .Append("20990101000000_Unexpected").ToArray();
        var unexpected = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bMigrationContract.RequireExact(extra, false));
        Assert.Equal(Arch7aArch7bMigrationContract.ExactSetMismatch,
            unexpected.Message);

        var pending = Assert.Throws<InvalidDataException>(() =>
            Arch7aArch7bMigrationContract.RequireExact(
                PmsShadowStateContract.MigrationIds, true));
        Assert.Equal(Arch7aArch7bMigrationContract.PendingModelChanges,
            pending.Message);
    }

    [Fact]
    public void Live_path_uses_preloaded_child_credential_and_has_no_secret_client()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7aShadowQualification",
            "Arch7aArch7bShadowQualification.cs"));
        Assert.Equal(1, Count(source, "Environment.GetEnvironmentVariable("));
        Assert.Contains("QQ_ARCH7B_POSITION_IMPORT_FAST_PATH", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetSecretValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretsManager", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEnvironmentVariable", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MigrateAsync", source, StringComparison.Ordinal);
        Assert.Contains("Arch7bPostgreSqlPinnedSessionFactory.Create", source,
            StringComparison.Ordinal);
        Assert.Contains("Arch7bPostgreSqlPinnedOpenSupervisor", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Pinned_readers_do_not_reopen_or_close_an_already_open_connection()
    {
        foreach (var file in new[]
                 {
                     "PmsShadowIntradayPersistence.cs",
                     "PmsShadowIntradayEconomicRefresh.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(
                RepositoryRoot(), "src",
                "QQ.Production.Intraday.Infrastructure.PostgreSql", file));
            Assert.Contains(
                "ownsConnectionLifecycle = connection.State != ConnectionState.Open",
                source, StringComparison.Ordinal);
            Assert.Contains("if (ownsConnectionLifecycle)", source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Output_is_content_addressed_and_contains_no_credential()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "arch7a-arch7b-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var evidence = new Arch7aArch7bShadowQualificationEvidence(
                Arch7aArch7bShadowQualificationArguments.ContractVersion,
                "ARCH7A_ARCH7B_SHADOW_QUALIFICATION_COMPLETED",
                "ARCH7B_RDS_TEST",
                Arch7aArch7bShadowQualificationArguments.TargetFingerprint,
                18,
                Arch7bPostgreSqlPinnedTransportProfile.DirectPrimaryProfile,
                Guid.NewGuid(),
                "slot",
                "session",
                new string('a', 64),
                Arch7aArch7bPrivilegeContract.LoginRole,
                Arch7aArch7bPrivilegeContract.QualificationRole,
                true, true, true,
                0, 0, 0, 0,
                "AMBIENT_PUBLIC_PRIVILEGE_ACCEPTED_NOT_DIRECTLY_GRANTED",
                new string('b', 64),
                new string('c', 64),
                7, 7, 7, 7,
                "Persisted",
                "AlreadyPersistedIdentical",
                new(7, 7, 7, 7, 1, "COMPLETED", true, true, true),
                true, true, true, true, 0, 1, 0, 1, 1, true, string.Empty);

            var written =
                Arch7aArch7bShadowQualificationRunner.WriteEvidence(
                    directory, evidence);
            var text = File.ReadAllText(Path.Combine(
                directory, "arch7a-shadow-qualification.json"));

            Assert.Equal(64, written.EvidenceSha256.Length);
            Assert.DoesNotContain("password", text,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connection_string", text,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static List<string> ValidArguments() =>
    [
        "--mode", "qualify-shadow",
        "--economic-revision-id", "11111111-1111-1111-1111-111111111111",
        "--slot-id", "arch7b-slot",
        "--source-session-id", "arch7b-session",
        "--position-market-revision-binding-path",
        @"C:\arch7b\position-market-revision-input-binding.json",
        "--expected-position-market-revision-binding-sha256",
        new string('a', 64),
        "--target-profile-id", "ARCH7B_RDS_TEST",
        "--expected-environment", "TEST",
        "--expected-database", "qq_pms_shadow_arch7b_test",
        "--expected-schema", "pms_shadow",
        "--expected-postgres-major", "18",
        "--require-tls", "true",
        "--allow-loopback", "false",
        "--root-certificate",
        Arch7aArch7bShadowQualificationArguments.CanonicalRootCertificatePath,
        "--expected-root-certificate-sha256",
        Arch7aArch7bShadowQualificationArguments.RootCertificateSha256,
        "--expected-target-fingerprint",
        Arch7aArch7bShadowQualificationArguments.TargetFingerprint,
        "--repository-commit", new string('b', 40),
        "--output-directory", @"C:\arch7b\output",
        "--connection-secret-reference",
        Arch7aArch7bShadowQualificationArguments.CredentialReference,
        "--role", Arch7aArch7bShadowQualificationArguments.DatabaseRole,
        "--no-order", "true",
        "--validate-command-contract-only", "true"
    ];

    private static string ResolveManifestValue(string name, string value) =>
        name switch
        {
            "--economic-revision-id" =>
                "11111111-1111-1111-1111-111111111111",
            "--slot-id" => "arch7b-slot",
            "--source-session-id" => "arch7b-session",
            "--position-market-revision-binding-path" =>
                @"C:\arch7b\position-market-revision-input-binding.json",
            "--expected-position-market-revision-binding-sha256" =>
                new string('a', 64),
            "--repository-commit" => new string('b', 40),
            "--output-directory" => @"C:\arch7b\output",
            "--validate-command-contract-only" => "true",
            _ => value
        };

    private static int Count(string value, string text)
    {
        var count = 0;
        for (var index = 0;
             (index = value.IndexOf(text, index, StringComparison.Ordinal)) >= 0;
             index += text.Length)
            count++;
        return count;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName, "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("test repository root not found");
    }
}
