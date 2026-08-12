using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCorePrequalificationChromeAuthorityTests
{
    [Fact]
    public void Primary_chrome_identity_is_pinned_exactly()
    {
        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Arch7bSealedNonSecretEnvironment.QualifiedChromeExecutablePath);
        Assert.Equal("1c8a72b0e6b5a4dd1de5ce42a7b11460753d8941baebda208360475f31eb17d2",
            Arch7bSealedNonSecretEnvironment.QualifiedChromeExecutableSha256);
        Assert.Equal("151.0.7922.110",
            Arch7bSealedNonSecretEnvironment.QualifiedChromeVersion);
    }

    [Fact]
    public void Core_prequalification_environment_contains_only_the_sealed_path()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();

        var first = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);
        var second = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);

        Assert.Equal(first, second);
        var path = Assert.Single(first);
        Assert.Equal("PATH", path.VariableName);
        Assert.DoesNotContain(first, value =>
            value.VariableName.Equals("ProgramFiles(x86)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Exact_chrome_authority_is_accepted()
    {
        var chrome = Arch7bTaskkillTestAuthorities.Create()["chrome_executable"];

        Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(
            chrome, chrome.Path, _ => false);
    }

    [Fact]
    public void Wrong_chrome_sha_is_rejected()
    {
        var chrome = Arch7bTaskkillTestAuthorities.Create()["chrome_executable"];

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(
                chrome with { Sha256 = new string('0', 64) }, chrome.Path, _ => false));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutableShaMismatch,
            error.Message);
    }

    [Fact]
    public void Non_chrome_executable_is_rejected_even_when_path_is_expected()
    {
        var node = Arch7bTaskkillTestAuthorities.Create()["node_executable"];
        var authority = node with { AuthorityId = "chrome_executable" };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(
                authority, authority.Path, _ => false));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutablePathAuthorityMismatch,
            error.Message);
    }

    [Fact]
    public void Wrong_chrome_path_is_rejected()
    {
        var chrome = Arch7bTaskkillTestAuthorities.Create()["chrome_executable"];
        var expected = Path.Combine(Path.GetDirectoryName(chrome.Path)!, "other", "chrome.exe");

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(
                chrome, expected, _ => false));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutablePathAuthorityMismatch,
            error.Message);
    }

    [Fact]
    public void Reparse_point_in_chrome_path_chain_is_rejected()
    {
        var chrome = Arch7bTaskkillTestAuthorities.Create()["chrome_executable"];
        var parent = Path.GetDirectoryName(chrome.Path)!;

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(
                chrome, chrome.Path, path => string.Equals(path, parent,
                    StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutablePathAuthorityMismatch,
            error.Message);
    }
}
