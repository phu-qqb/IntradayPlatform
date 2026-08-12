using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCorePrequalificationTaskkillPathTests
{
    [Fact]
    public void Exact_taskkill_authority_and_three_directory_path_are_accepted()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var value = Assert.Single(Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationExecutableSearchPath(authorities));

        Assert.Equal("PATH", value.VariableName);
        Assert.Equal(Arch7bSealedNonSecretEnvironment.CorePrequalificationPathAuthorityId,
            value.SourceAuthorityId);
        Assert.Equal(string.Join(Path.PathSeparator,
            Path.GetDirectoryName(authorities["git_executable"].Path),
            Path.GetDirectoryName(authorities["node_executable"].Path),
            Environment.GetFolderPath(Environment.SpecialFolder.System)), value.Value);
        Arch7bSealedNonSecretEnvironment.ValidateMaterialized(
            [value], authorities["node_executable"].Path);
    }

    [Fact]
    public void Missing_taskkill_authority_is_rejected()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        authorities.Remove("taskkill_executable");

        Assert.Equal(Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment
                    .ForCorePrequalificationExecutableSearchPath(authorities)).BlockerCode);
    }

    [Fact]
    public void Wrong_taskkill_path_filename_parent_and_sha_are_rejected()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var taskkill = authorities["taskkill_executable"];
        var wrongPath = new Dictionary<string, Arch7bFileAuthority>(authorities, StringComparer.Ordinal)
        {
            ["taskkill_executable"] = taskkill with
            {
                Path = authorities["node_executable"].Path,
                Sha256 = authorities["node_executable"].Sha256
            }
        };
        AssertPathBlocker(wrongPath);

        var wrongName = new Dictionary<string, Arch7bFileAuthority>(authorities, StringComparer.Ordinal)
        {
            ["taskkill_executable"] = taskkill with
            {
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "cmd.exe"),
                Sha256 = Arch7bTaskkillTestAuthorities.Sha(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
            }
        };
        AssertPathBlocker(wrongName);

        var wrongSha = new Dictionary<string, Arch7bFileAuthority>(authorities, StringComparer.Ordinal)
        {
            ["taskkill_executable"] = taskkill with { Sha256 = new string('0', 64) }
        };
        Assert.Equal(Arch7bV2Blockers.CommandTaskkillExecutableShaMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment
                    .ForCorePrequalificationExecutableSearchPath(wrongSha)).BlockerCode);
    }

    [Fact]
    public void Taskkill_file_and_parent_reparse_points_are_rejected()
    {
        var taskkill = Arch7bTaskkillTestAuthorities.Create()["taskkill_executable"];
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);

        Assert.Equal(Arch7bV2Blockers.CommandTaskkillExecutablePathAuthorityMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ValidateTaskkillAuthority(
                    taskkill, system, path => string.Equals(path, taskkill.Path,
                        StringComparison.OrdinalIgnoreCase))).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.CommandTaskkillExecutablePathAuthorityMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ValidateTaskkillAuthority(
                    taskkill, system, path => string.Equals(path, system,
                        StringComparison.OrdinalIgnoreCase))).BlockerCode);
    }

    [Fact]
    public void Incomplete_extended_reordered_and_ambient_paths_are_rejected()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var value = Assert.Single(Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationExecutableSearchPath(authorities));
        var entries = value.Value.Split(Path.PathSeparator);

        AssertRejected(value with { Value = string.Join(Path.PathSeparator, entries.Take(2)) },
            authorities);
        AssertRejected(value with { Value = string.Join(Path.PathSeparator,
            entries.Concat([Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)])) }, authorities);
        AssertRejected(value with { Value = string.Join(Path.PathSeparator,
            new[] { entries[1], entries[0], entries[2] }) }, authorities);
        AssertRejected(value with { Value = value.Value + Path.PathSeparator +
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty) }, authorities);
    }

    [Fact]
    public void Composite_path_evidence_changes_with_every_executable_authority()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var value = Assert.Single(Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationExecutableSearchPath(authorities));

        foreach (var id in Arch7bSealedNonSecretEnvironment
                     .CorePrequalificationPathSourceAuthorityIds)
        {
            var changed = new Dictionary<string, Arch7bFileAuthority>(authorities,
                StringComparer.Ordinal)
            {
                [id] = authorities[id] with { Sha256 = new string('0', 64) }
            };
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment
                    .ForCorePrequalificationExecutableSearchPath(changed));
        }

        var second = Assert.Single(Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationExecutableSearchPath(authorities));
        Assert.Equal(value, second);
    }

    private static void AssertPathBlocker(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities) =>
        Assert.Equal(Arch7bV2Blockers.CommandTaskkillExecutablePathAuthorityMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment
                    .ForCorePrequalificationExecutableSearchPath(authorities)).BlockerCode);

    private static void AssertRejected(Arch7bSealedNonSecretEnvironmentVariable value,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateTemplate([value], authorities));
        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateMaterialized(
                [value], authorities["node_executable"].Path));
    }
}
