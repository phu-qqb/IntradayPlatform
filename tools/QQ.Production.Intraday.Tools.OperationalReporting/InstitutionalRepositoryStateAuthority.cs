using System.Diagnostics;
using System.Reflection;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public sealed record InstitutionalRepositoryRawState(
    string RepositoryRoot,
    string ActualHead,
    bool WorktreeClean,
    bool IndexClean,
    bool RoadmapTracked,
    string WorktreeRoadmapBlobId,
    string HeadRoadmapBlobId,
    string? BuildCommit);

public interface IInstitutionalRepositoryStateProbe
{
    InstitutionalRepositoryRawState Read(string repositoryRoot, string roadmapRelativePath);
}

public sealed record InstitutionalRepositoryStateAuthorityResult(
    string ContractVersion,
    string RepositoryRootIdentitySha256,
    string ActualHead,
    bool WorktreeClean,
    bool IndexClean,
    bool RoadmapTrackedAtHead,
    string RoadmapBlobId,
    string? BuildCommit,
    string EvidenceSha256);

public static class InstitutionalRepositoryStateAuthority
{
    public const string ContractVersion = "institutional_repository_state_authority_v1";

    public static InstitutionalRepositoryStateAuthorityResult Resolve(
        string repositoryRoot,
        string? suppliedCommit,
        IInstitutionalRepositoryStateProbe? probe = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new InvalidDataException("RPT2_REPOSITORY_ROOT_REQUIRED");
        var requestedRoot = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var raw = (probe ?? new GitInstitutionalRepositoryStateProbe()).Read(
            requestedRoot, InstitutionalRoadmapAuthority.RelativePath);
        var actualRoot = Path.GetFullPath(raw.RepositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(requestedRoot, actualRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RPT2_REPOSITORY_ROOT_NOT_GIT_ROOT");
        if (!IsGitCommit(raw.ActualHead))
            throw new InvalidDataException("RPT2_REPOSITORY_HEAD_INVALID");
        if (suppliedCommit is not null &&
            !string.Equals(suppliedCommit, raw.ActualHead, StringComparison.Ordinal))
            throw new InvalidDataException("RPT2_REPOSITORY_COMMIT_MISMATCH");
        if (!raw.WorktreeClean || !raw.IndexClean)
            throw new InvalidDataException("RPT2_REPOSITORY_WORKTREE_NOT_CLEAN");
        if (!raw.RoadmapTracked ||
            !string.Equals(raw.WorktreeRoadmapBlobId, raw.HeadRoadmapBlobId,
                StringComparison.Ordinal))
            throw new InvalidDataException("RPT2_ROADMAP_NOT_AT_HEAD");
        if (raw.BuildCommit is not null &&
            !string.Equals(raw.BuildCommit, raw.ActualHead, StringComparison.Ordinal))
            throw new InvalidDataException("RPT2_REPOSITORY_BUILD_COMMIT_MISMATCH");

        var rootIdentity = Arch5bHashing.HashCanonical(new
        {
            ContractVersion,
            RootName = new DirectoryInfo(actualRoot).Name,
            RootPathHash = Arch5bHashing.Sha256Hex(
                actualRoot.Replace('\\', '/').ToUpperInvariant())
        });
        var evidence = Arch5bHashing.HashCanonical(new
        {
            ContractVersion,
            RepositoryRootIdentitySha256 = rootIdentity,
            raw.ActualHead,
            raw.WorktreeClean,
            raw.IndexClean,
            raw.RoadmapTracked,
            RoadmapBlobId = raw.HeadRoadmapBlobId,
            raw.BuildCommit
        });
        return new(
            ContractVersion,
            rootIdentity,
            raw.ActualHead,
            raw.WorktreeClean,
            raw.IndexClean,
            raw.RoadmapTracked,
            raw.HeadRoadmapBlobId,
            raw.BuildCommit,
            evidence);
    }

    private static bool IsGitCommit(string value) =>
        value.Length is 40 or 64 &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));
}

public sealed class GitInstitutionalRepositoryStateProbe : IInstitutionalRepositoryStateProbe
{
    public InstitutionalRepositoryRawState Read(
        string repositoryRoot,
        string roadmapRelativePath)
    {
        if (!Directory.Exists(repositoryRoot))
            throw new InvalidDataException("RPT2_REPOSITORY_ROOT_NOT_GIT");
        var actualRoot = Git(repositoryRoot, "rev-parse", "--show-toplevel").Trim();
        var head = Git(repositoryRoot, "rev-parse", "HEAD").Trim();
        var status = Git(repositoryRoot, "status", "--porcelain=v1", "--untracked-files=all");
        var indexClean = GitExitCode(repositoryRoot, "diff", "--cached", "--quiet") == 0;
        var tracked = GitExitCode(
            repositoryRoot, "ls-files", "--error-unmatch", "--", roadmapRelativePath) == 0;
        var worktreeBlob = tracked
            ? Git(repositoryRoot, "hash-object",
                Path.Combine(repositoryRoot,
                    roadmapRelativePath.Replace('/', Path.DirectorySeparatorChar))).Trim()
            : string.Empty;
        var headBlob = tracked
            ? Git(repositoryRoot, "rev-parse", $"HEAD:{roadmapRelativePath}").Trim()
            : string.Empty;
        return new(
            actualRoot,
            head,
            string.IsNullOrWhiteSpace(status),
            indexClean,
            tracked,
            worktreeBlob,
            headBlob,
            BuildCommit());
    }

    private static string? BuildCommit()
    {
        var version = typeof(GitInstitutionalRepositoryStateProbe).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var separator = version?.LastIndexOf('+') ?? -1;
        if (separator < 0 || separator == version!.Length - 1)
            return null;
        var candidate = version[(separator + 1)..];
        return candidate.Length is 40 or 64 &&
               candidate.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character))
            ? candidate
            : null;
    }

    private static string Git(string root, params string[] arguments)
    {
        var (exitCode, output, error) = Run(root, arguments);
        if (exitCode != 0)
            throw new InvalidDataException(
                $"RPT2_REPOSITORY_ROOT_NOT_GIT:{error.Trim()}");
        return output;
    }

    private static int GitExitCode(string root, params string[] arguments) =>
        Run(root, arguments).ExitCode;

    private static (int ExitCode, string Output, string Error) Run(
        string root,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(root);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)
            ?? throw new InvalidDataException("RPT2_REPOSITORY_GIT_UNAVAILABLE");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }
}
