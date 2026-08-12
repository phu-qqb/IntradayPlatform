using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bNonSecretEnvironmentValueKind
{
    AbsoluteDirectory,
    ExecutableSearchPath
}

public sealed record Arch7bSealedNonSecretEnvironmentVariable(
    string ContractVersion,
    string VariableName,
    Arch7bNonSecretEnvironmentValueKind ValueKind,
    string Value,
    string SourceAuthorityId,
    string SourceAuthoritySha256,
    string EvidenceSha256);

public static class Arch7bSealedNonSecretEnvironment
{
    public const string CorePrequalificationPathAuthorityId =
        "core_prequalification_executable_search_path";
    public static readonly IReadOnlyList<string> CorePrequalificationPathSourceAuthorityIds =
        ["git_executable", "node_executable", "taskkill_executable"];

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ForDotnetRoot(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var root = Require(authorities, "dotnet_root", Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        var executable = Require(authorities, "dotnet_executable", Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        ValidateDotnetAuthorities(root, executable);
        var canonical = string.Join('\n', Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            "DOTNET_ROOT", Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory, root.Path,
            root.AuthorityId, root.Sha256);
        return [new(Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion, "DOTNET_ROOT",
            Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory, root.Path, root.AuthorityId,
            root.Sha256, Arch7bOneShotContracts.Sha256(canonical))];
    }

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable>
        ForCorePrequalificationExecutableSearchPath(
            IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var git = Require(authorities, "git_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        var node = Require(authorities, "node_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        var taskkill = Require(authorities, "taskkill_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        ValidateExecutableAuthority(git, Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch,
            Arch7bV2Blockers.CommandGitExecutableShaMismatch);
        ValidateExecutableAuthority(node, Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch,
            Arch7bV2Blockers.CommandNodeExecutableShaMismatch);
        ValidateTaskkillAuthority(taskkill);

        var directories = new[] { Parent(git), Parent(node), Parent(taskkill) };
        if (directories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        var searchPath = string.Join(Path.PathSeparator, directories);
        var sourceCanonical = SourceCanonical(git, node, taskkill);
        var sourceSha = Arch7bOneShotContracts.Sha256(sourceCanonical);
        var canonical = PathCanonical(searchPath, sourceCanonical, sourceSha);
        return [new(Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion, "PATH",
            Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath, searchPath,
            CorePrequalificationPathAuthorityId, sourceSha,
            Arch7bOneShotContracts.Sha256(canonical))];
    }

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ValidateTemplate(
        IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        if (values.Count == 0) return values;
        if (values.Count > 2 ||
            values.Select(value => value.VariableName).Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
        foreach (var value in values)
        {
            var (expected, blocker) = value.VariableName switch
            {
                "DOTNET_ROOT" => (ForDotnetRoot(authorities).Single(),
                    Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch),
                "PATH" => (ForCorePrequalificationExecutableSearchPath(authorities).Single(),
                    Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch),
                _ => throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden)
            };
            if (value != expected) throw new Arch7bQualificationException(blocker);
        }
        return values;
    }

    public static void ValidateMaterialized(IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values,
        string? executablePath = null)
    {
        foreach (var value in values)
        {
            var expectedSource = value.VariableName switch
            {
                "DOTNET_ROOT" => "dotnet_root",
                "PATH" => CorePrequalificationPathAuthorityId,
                _ => null
            };
            var validValue = value.VariableName switch
            {
                "DOTNET_ROOT" => value.ValueKind == Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory &&
                    Path.IsPathFullyQualified(value.Value),
                "PATH" => value.ValueKind == Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath &&
                    ValidateMaterializedCorePath(value, executablePath),
                _ => false
            };
            if (value.ContractVersion != Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion ||
                expectedSource is null || value.SourceAuthorityId != expectedSource || !validValue ||
                !Arch7bOneShotContracts.IsSha256(value.SourceAuthoritySha256))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
            var canonical = value.VariableName == "PATH"
                ? MaterializedPathCanonical(value, executablePath)
                : string.Join('\n', value.ContractVersion, value.VariableName, value.ValueKind,
                    value.Value, value.SourceAuthorityId, value.SourceAuthoritySha256);
            if (value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(canonical))
                throw new Arch7bQualificationException(value.VariableName == "PATH"
                    ? Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch
                    : Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        }
        if (values.Count > 2 || values.Select(value => value.VariableName)
                .Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
    }

    public static string Canonical(IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values) =>
        string.Join('|', values.OrderBy(value => value.VariableName, StringComparer.Ordinal)
            .Select(value => string.Join(':', value.VariableName, value.ValueKind, value.Value,
                value.SourceAuthorityId, value.SourceAuthoritySha256, value.EvidenceSha256)));

    internal static void ValidateTaskkillAuthority(Arch7bFileAuthority authority,
        string? expectedSystemDirectory = null, Func<string, bool>? reparsePoint = null)
    {
        expectedSystemDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.System);
        reparsePoint ??= IsReparsePoint;
        var expectedPath = Path.Combine(expectedSystemDirectory, "taskkill.exe");
        if (!authority.MustExist || authority.MustBeInsideRunRoot ||
            !Path.IsPathFullyQualified(authority.Path) ||
            !string.Equals(Path.GetFullPath(authority.Path), Path.GetFullPath(expectedPath),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(authority.Path), "taskkill.exe",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(authority.Path) || Directory.Exists(authority.Path) ||
            !Directory.Exists(expectedSystemDirectory) || reparsePoint(authority.Path) ||
            reparsePoint(expectedSystemDirectory))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTaskkillExecutablePathAuthorityMismatch);
        if (FileSha(authority.Path) != authority.Sha256)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTaskkillExecutableShaMismatch);
    }

    private static bool ValidateMaterializedCorePath(
        Arch7bSealedNonSecretEnvironmentVariable value, string? executablePath)
    {
        try
        {
            var authorities = MaterializedAuthorities(value.Value, executablePath);
            ValidateExecutableAuthority(authorities["git_executable"],
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch,
                Arch7bV2Blockers.CommandGitExecutableShaMismatch);
            ValidateExecutableAuthority(authorities["node_executable"],
                Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch,
                Arch7bV2Blockers.CommandNodeExecutableShaMismatch);
            ValidateTaskkillAuthority(authorities["taskkill_executable"]);
            var source = SourceCanonical(authorities["git_executable"],
                authorities["node_executable"], authorities["taskkill_executable"]);
            return value.SourceAuthoritySha256 == Arch7bOneShotContracts.Sha256(source);
        }
        catch (Exception error) when (error is Arch7bQualificationException or IOException or
                                      UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static string MaterializedPathCanonical(
        Arch7bSealedNonSecretEnvironmentVariable value, string? executablePath)
    {
        try
        {
            var authorities = MaterializedAuthorities(value.Value, executablePath);
            var source = SourceCanonical(authorities["git_executable"],
                authorities["node_executable"], authorities["taskkill_executable"]);
            return PathCanonical(value.Value, source, Arch7bOneShotContracts.Sha256(source));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, Arch7bFileAuthority> MaterializedAuthorities(
        string searchPath, string? executablePath)
    {
        if (executablePath is null || !File.Exists(executablePath))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch);
        var directories = searchPath.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (directories.Length != 3 || directories.Distinct(
                StringComparer.OrdinalIgnoreCase).Count() != 3 || directories.Any(directory =>
                !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory) ||
                IsReparsePoint(directory)))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(executablePath)), directories[1],
                StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch);
        var gitPath = Path.Combine(directories[0], "git.exe");
        var taskkillPath = Path.Combine(directories[2], "taskkill.exe");
        return new(StringComparer.Ordinal)
        {
            ["git_executable"] = new("git_executable", gitPath, FileSha(gitPath), true, false),
            ["node_executable"] = new("node_executable", executablePath,
                FileSha(executablePath), true, false),
            ["taskkill_executable"] = new("taskkill_executable", taskkillPath,
                FileSha(taskkillPath), true, false)
        };
    }

    private static Arch7bFileAuthority Require(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities, string id, string blocker)
    {
        if (!authorities.TryGetValue(id, out var value))
            throw new Arch7bQualificationException(blocker, id);
        return value;
    }

    private static void ValidateDotnetAuthorities(
        Arch7bFileAuthority root, Arch7bFileAuthority executable)
    {
        if (!root.MustExist || !executable.MustExist || !Path.IsPathFullyQualified(root.Path) ||
            !Path.IsPathFullyQualified(executable.Path) || !Directory.Exists(root.Path) ||
            !File.Exists(executable.Path) || IsReparsePoint(root.Path) || IsReparsePoint(executable.Path) ||
            !string.Equals(Path.GetFullPath(executable.Path), Path.Combine(Path.GetFullPath(root.Path), "dotnet.exe"),
                StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        if (FileSha(executable.Path) != executable.Sha256)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetExecutableShaMismatch);
    }

    private static void ValidateExecutableAuthority(Arch7bFileAuthority executable,
        string pathBlocker, string shaBlocker)
    {
        if (!executable.MustExist || executable.MustBeInsideRunRoot ||
            !Path.IsPathFullyQualified(executable.Path) || !File.Exists(executable.Path) ||
            Directory.Exists(executable.Path) || IsReparsePoint(executable.Path))
            throw new Arch7bQualificationException(pathBlocker);
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable.Path));
        if (directory is null || !Directory.Exists(directory) || IsReparsePoint(directory))
            throw new Arch7bQualificationException(pathBlocker);
        if (FileSha(executable.Path) != executable.Sha256)
            throw new Arch7bQualificationException(shaBlocker);
    }

    private static string Parent(Arch7bFileAuthority value) =>
        Path.GetDirectoryName(Path.GetFullPath(value.Path)) ??
        throw new Arch7bQualificationException(
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);

    private static string SourceCanonical(params Arch7bFileAuthority[] values) =>
        string.Join('|', values.Select(value => string.Join(':', value.AuthorityId,
            Path.GetFullPath(value.Path), value.Sha256)));

    private static string PathCanonical(string searchPath, string sourceCanonical, string sourceSha) =>
        string.Join('\n', Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            "PATH", Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath, searchPath,
            CorePrequalificationPathAuthorityId, sourceSha, sourceCanonical);

    private static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}