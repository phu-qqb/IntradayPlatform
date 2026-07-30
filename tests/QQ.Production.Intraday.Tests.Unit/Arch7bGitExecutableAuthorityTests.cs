using System.Diagnostics;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bGitExecutableAuthorityTests
{
    [Fact]
    public void Qualified_absolute_identity_is_accepted_without_shell_or_path()
    {
        if (!OperatingSystem.IsWindows()) return;
        var facts = Facts();

        var result = new Arch7bGitExecutableAuthorityQualifier(
            new FakeInspector(facts)).Qualify(
            facts.Path,
            facts.Sha256,
            facts.GitVersion,
            Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
            Arch7bGitExecutableAuthorityContract.ExecutionHostName);

        Assert.Equal(Arch7bGitExecutableAuthorityContract.Version,
            result.ContractVersion);
        Assert.False(result.UseShellExecute);
        Assert.False(result.ShellUsed);
        Assert.False(result.AmbientPathUsed);
        Assert.Equal(10, result.CommandTimeoutSeconds);
    }

    [Theory]
    [InlineData("git")]
    [InlineData("tools\\git.exe")]
    public void Short_or_relative_git_path_is_rejected(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        var failure = Assert.Throws<InvalidDataException>(() =>
            new Arch7bGitExecutableAuthorityQualifier(
                new FakeInspector(Facts())).Qualify(
                path, Hash('a'), Version,
                Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
                Arch7bGitExecutableAuthorityContract.ExecutionHostName));
        Assert.Equal(Arch7bGitExecutableAuthorityContract.PathNotAbsolute,
            failure.Message);
    }

    [Fact]
    public void Missing_absolute_executable_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(),
            Guid.NewGuid().ToString("N"), "git.exe");
        var failure = Assert.Throws<InvalidDataException>(() =>
            new Arch7bGitExecutableAuthorityQualifier().Qualify(
                path, Hash('a'), Version,
                Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
                Arch7bGitExecutableAuthorityContract.ExecutionHostName));
        Assert.Equal(Arch7bGitExecutableAuthorityContract.Missing,
            failure.Message);
    }

    [Theory]
    [InlineData("sha")]
    [InlineData("version")]
    [InlineData("reparse")]
    [InlineData("fake")]
    public void Altered_git_identity_is_rejected(string mutation)
    {
        if (!OperatingSystem.IsWindows()) return;
        var facts = Facts() with
        {
            HasReparsePoint = mutation == "reparse",
            Architecture = mutation == "fake" ? "x86" : "x64"
        };
        var expectedSha = mutation == "sha" ? Hash('b') : facts.Sha256;
        var expectedVersion = mutation == "version"
            ? "git version 0.0.0"
            : facts.GitVersion;

        var failure = Assert.Throws<InvalidDataException>(() =>
            new Arch7bGitExecutableAuthorityQualifier(
                new FakeInspector(facts)).Qualify(
                facts.Path, expectedSha, expectedVersion,
                Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
                Arch7bGitExecutableAuthorityContract.ExecutionHostName));

        Assert.Equal(mutation switch
        {
            "sha" => Arch7bGitExecutableAuthorityContract.ShaMismatch,
            "version" => Arch7bGitExecutableAuthorityContract.VersionMismatch,
            "reparse" =>
                Arch7bGitExecutableAuthorityContract.ReparsePointRejected,
            _ => "ARCH7B_GIT_EXECUTABLE_ARCHITECTURE_MISMATCH"
        }, failure.Message);
    }

    [Fact]
    public void Different_candidate_identities_are_ambiguous()
    {
        var failure = Assert.Throws<InvalidDataException>(() =>
            Arch7bGitExecutableCandidateAuthority.Select(
            [
                Facts(),
                Facts() with { Path = "C:\\Other\\git.exe", Sha256 = Hash('b') }
            ]));
        Assert.Equal(Arch7bGitExecutableAuthorityContract.IdentityAmbiguous,
            failure.Message);
    }

    [Fact]
    public void Explicit_git_runner_reads_exact_head_and_repository()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var repository = TestRepository.Create();
        var state = new GitArch7bRepositoryStateAuthority().Resolve(
            repository.Root, repository.Head, repository.Authority);
        Assert.Equal(repository.Head, state.HeadCommit);
        Assert.True(state.WorktreeClean);
        Assert.True(state.IndexClean);
    }

    [Fact]
    public void Dirty_worktree_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var repository = TestRepository.Create();
        File.AppendAllText(Path.Combine(repository.Root, "tracked.txt"), "x");
        AssertRepositoryRejected(repository);
    }

    [Fact]
    public void Dirty_index_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var repository = TestRepository.Create();
        File.AppendAllText(Path.Combine(repository.Root, "tracked.txt"), "x");
        repository.Git("add", "tracked.txt");
        AssertRepositoryRejected(repository);
    }

    [Fact]
    public void Wrong_build_commit_or_remote_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var repository = TestRepository.Create();
        Assert.Throws<InvalidDataException>(() =>
            new GitArch7bRepositoryStateAuthority().Resolve(
                repository.Root, new string('a', 40), repository.Authority));
        repository.Git("remote", "set-url", "origin",
            "https://example.invalid/wrong.git");
        AssertRepositoryRejected(repository);
    }

    [Fact]
    public void Cli_qualifies_repository_before_runtime_secret_and_open()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
            "Program.cs"));
        var qualify = source.IndexOf(
            "qualify-repository-authority", StringComparison.Ordinal);
        var runtime = source.IndexOf(
            "arguments.BuildRuntime()", StringComparison.Ordinal);
        var open = source.IndexOf(
            "supervisor.StartOpen()", StringComparison.Ordinal);
        Assert.True(qualify >= 0 && qualify < runtime && runtime < open);
        Assert.Contains(
            "Arch7bGitExecutableAuthorityContract.ArgumentRequired",
            source, StringComparison.Ordinal);
        Assert.Contains("--git-executable", source, StringComparison.Ordinal);
        Assert.Contains("--expected-git-sha256",
            source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_source_has_no_shell_or_ambient_path_lookup()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bGitExecutableAuthority.cs"));
        Assert.Contains("UseShellExecute = false", source,
            StringComparison.Ordinal);
        Assert.Contains("CreateNoWindow = true", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("cmd.exe", source, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", source,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Environment[\"PATH\"]", source,
            StringComparison.Ordinal);
    }

    private static void AssertRepositoryRejected(TestRepository repository) =>
        Assert.Throws<InvalidDataException>(() =>
            new GitArch7bRepositoryStateAuthority().Resolve(
                repository.Root, repository.Head, repository.Authority));

    private static Arch7bGitExecutableFacts Facts() =>
        new("C:\\MiniGit\\cmd\\git.exe", Hash('a'), Version,
            "x64", "Valid", true, false);

    private const string Version = "git version 2.55.0.windows.3";

    private static string Hash(char value) => new(value, 64);

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".git")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FakeInspector(Arch7bGitExecutableFacts facts) :
        IArch7bGitExecutableInspector
    {
        public Arch7bGitExecutableFacts Inspect(string path) => facts;
    }

    private sealed class TestRepository : IDisposable
    {
        private TestRepository(
            string root,
            string executable,
            Arch7bGitExecutableAuthority authority,
            string head)
        {
            Root = root;
            Executable = executable;
            Authority = authority;
            Head = head;
        }

        public string Root { get; }
        public string Executable { get; }
        public Arch7bGitExecutableAuthority Authority { get; }
        public string Head { get; }

        public static TestRepository Create()
        {
            var git = FindGit();
            var root = Path.Combine(Path.GetTempPath(),
                "arch7b-git-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Run(git, root, "init");
            Run(git, root, "config", "user.name", "ARCH7B Test");
            Run(git, root, "config", "user.email", "arch7b@example.invalid");
            File.WriteAllText(Path.Combine(root, "tracked.txt"), "initial");
            Run(git, root, "add", "tracked.txt");
            Run(git, root, "commit", "-m", "initial");
            Run(git, root, "remote", "add", "origin",
                Arch7bGitExecutableAuthorityContract.ExpectedRepositoryRemote);
            var head = Run(git, root, "rev-parse", "HEAD");
            var authority = new Arch7bGitExecutableAuthority(
                Arch7bGitExecutableAuthorityContract.Version,
                Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
                Arch7bGitExecutableAuthorityContract.ExecutionHostName,
                git, "test", "test", "x64", "Valid",
                false, false, false, 10);
            return new(root, git, authority, head);
        }

        public string Git(params string[] arguments) =>
            Run(Executable, Root, arguments);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                // Sandbox ACL cleanup is performed by the test harness.
            }
        }

        private static string FindGit()
        {
            using var process = Process.Start(new ProcessStartInfo("where.exe")
            {
                Arguments = "git",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            var path = process.StandardOutput.ReadLine();
            process.WaitForExit();
            return Path.GetFullPath(path ??
                throw new FileNotFoundException("git.exe not found"));
        }

        private static string Run(
            string executable, string root, params string[] arguments) =>
            Arch7bBoundedGitProcess.Run(
                executable, root, arguments, allowEmpty: true).Output;
    }
}
