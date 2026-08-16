using System.Security.Cryptography;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLiveCliAuthorityBindingValidatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "qq-arch7b-cli-authority-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Target_bound_directories_accept_distinct_portable_authority_shas()
    {
        var fixture = CreateFixture();

        var result = Arch7bLiveCliAuthorityBindingValidator.Validate(
            fixture.Template, fixture.CliPaths);

        Assert.Equal(4, result.ValidatedBindingCount);
        Assert.Equal(0, result.PathMismatchCount);
        Assert.Equal(0, result.FileContentShaMismatchCount);
        Assert.Equal("TARGET_DIRECTORY_INVENTORY_SHA256_VALIDATED_BY_STATIC_AUTHORITY",
            result.Bindings.Single(value => value.AuthorityId == "core_repository").ShaSemantics);
        Assert.NotEqual(fixture.Template.CoreRepositoryAuthoritySha256,
            fixture.Template.FileAuthorities["core_repository"].Sha256);
        Assert.NotEqual(fixture.Template.RuntimeInventorySha256,
            fixture.Template.FileAuthorities["intraday_runtime"].Sha256);
    }

    [Theory]
    [InlineData("core_repository")]
    [InlineData("intraday_runtime")]
    public void Wrong_directory_cli_path_is_rejected(string authorityId)
    {
        var fixture = CreateFixture();
        var paths = new Dictionary<string, string>(fixture.CliPaths, StringComparer.Ordinal)
        {
            [authorityId] = Directory.CreateDirectory(Path.Combine(root,
                authorityId + "-wrong")).FullName
        };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bLiveCliAuthorityBindingValidator.Validate(fixture.Template, paths));

        Assert.Equal(Arch7bV2Blockers.LiveCliAuthorityPathMismatch, error.BlockerCode);
        Assert.Contains(authorityId + ":path", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("git_executable")]
    [InlineData("root_certificate")]
    public void Wrong_file_content_sha_is_rejected(string authorityId)
    {
        var fixture = CreateFixture();
        File.AppendAllText(fixture.CliPaths[authorityId], "changed");

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bLiveCliAuthorityBindingValidator.Validate(
                fixture.Template, fixture.CliPaths));

        Assert.Equal(Arch7bV2Blockers.LiveCliAuthorityFileContentShaMismatch,
            error.BlockerCode);
        Assert.Contains(authorityId + ":file-content", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Static_preflight_and_live_path_share_one_cli_validator()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "Arch7bProgramV2Modes.cs"));

        Assert.Equal(2, Count(source, "ValidateCliAuthorities(options, template.Value)"));
        Assert.DoesNotContain("BindCliAuthority(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Directory_content_validation_remains_owned_by_static_validator()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "Arch7bOperationalExecutionAuthorities.cs"));

        Assert.Contains("ValidateDirectory(source);", source, StringComparison.Ordinal);
        Assert.Contains("ValidateGitRepository(source, gitExecutable);", source,
            StringComparison.Ordinal);
        Assert.Contains("actual.EvidenceSha256 != stored.EvidenceSha256", source,
            StringComparison.Ordinal);
    }

    private (Arch7bOneShotLivePlanTemplate Template,
        IReadOnlyDictionary<string, string> CliPaths) CreateFixture()
    {
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "supervisor.exe");
        File.WriteAllText(executable, "supervisor");
        var fixture = Arch7bV2QualificationFactory.Create(executable,
            Path.Combine(root, "run"));
        var core = Directory.CreateDirectory(Path.Combine(root, "core")).FullName;
        var intraday = Directory.CreateDirectory(Path.Combine(root, "intraday")).FullName;
        var git = Path.Combine(root, "git.exe");
        var rootCertificate = Path.Combine(root, "root.pem");
        File.WriteAllText(git, "git");
        File.WriteAllText(rootCertificate, "root-ca");
        var authorities = new Dictionary<string, Arch7bFileAuthority>(
            fixture.Template.FileAuthorities, StringComparer.Ordinal)
        {
            ["core_repository"] = new("core_repository", core,
                Hash("target-core-directory-inventory"), true, false),
            ["intraday_runtime"] = new("intraday_runtime", intraday,
                Hash("target-intraday-directory-inventory"), true, false),
            ["git_executable"] = new("git_executable", git, FileSha(git), true, false),
            ["root_certificate"] = new("root_certificate", rootCertificate,
                FileSha(rootCertificate), true, false)
        };
        var template = fixture.Template with
        {
            FileAuthorities = authorities,
            RuntimeInventorySha256 = Hash("portable-runtime-inventory"),
            CoreRepositoryAuthoritySha256 = Hash("portable-core-authority")
        };
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["core_repository"] = core,
            ["intraday_runtime"] = intraday,
            ["git_executable"] = git,
            ["root_certificate"] = rootCertificate
        };
        return (template, paths);
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static string Hash(string value) => Arch7bOneShotContracts.Sha256(value);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
