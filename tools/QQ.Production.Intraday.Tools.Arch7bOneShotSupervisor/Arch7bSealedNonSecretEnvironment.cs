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

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ForGitAndNodeExecutablePath(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var gitExecutable = Require(authorities, "git_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        var nodeExecutable = Require(authorities, "node_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        ValidateExecutableAuthority(gitExecutable,
            Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch,
            Arch7bV2Blockers.CommandGitExecutableShaMismatch);
        ValidateExecutableAuthority(nodeExecutable,
            Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch,
            Arch7bV2Blockers.CommandNodeExecutableShaMismatch);
        var gitDirectory = Path.GetDirectoryName(Path.GetFullPath(gitExecutable.Path)) ??
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        var nodeDirectory = Path.GetDirectoryName(Path.GetFullPath(nodeExecutable.Path)) ??
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandNodeExecutablePathAuthorityMismatch);
        var searchPath = string.Join(Path.PathSeparator, new[] { gitDirectory, nodeDirectory }
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var canonical = string.Join('\n', Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            "PATH", Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath, searchPath,
            gitExecutable.AuthorityId, gitExecutable.Sha256);
        return [new(Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion, "PATH",
            Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath, searchPath,
            gitExecutable.AuthorityId, gitExecutable.Sha256,
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
                "PATH" => (ForGitAndNodeExecutablePath(authorities).Single(),
                    Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch),
                _ => throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden)
            };
            if (value != expected)
                throw new Arch7bQualificationException(blocker);
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
                "PATH" => "git_executable",
                _ => null
            };
            var validValue = value.VariableName switch
            {
                "DOTNET_ROOT" => value.ValueKind == Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory &&
                    Path.IsPathFullyQualified(value.Value),
                "PATH" => value.ValueKind == Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath &&
                    ValidExecutableSearchPath(value.Value, executablePath),
                _ => false
            };
            if (value.ContractVersion != Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion ||
                expectedSource is null || value.SourceAuthorityId != expectedSource || !validValue ||
                !Arch7bOneShotContracts.IsSha256(value.SourceAuthoritySha256))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
            var canonical = string.Join('\n', value.ContractVersion, value.VariableName, value.ValueKind,
                value.Value, value.SourceAuthorityId, value.SourceAuthoritySha256);
            if (value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(canonical))
                throw new Arch7bQualificationException(value.VariableName == "PATH"
                    ? Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch
                    : Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        }
        if (values.Count > 2 || values.Select(value => value.VariableName).Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
    }

    public static string Canonical(IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values) =>
        string.Join('|', values.OrderBy(value => value.VariableName, StringComparer.Ordinal)
            .Select(value => string.Join(':', value.VariableName, value.ValueKind, value.Value,
                value.SourceAuthorityId, value.SourceAuthoritySha256, value.EvidenceSha256)));

    private static Arch7bFileAuthority Require(IReadOnlyDictionary<string, Arch7bFileAuthority> authorities,
        string id, string blocker)
    {
        if (!authorities.TryGetValue(id, out var value))
            throw new Arch7bQualificationException(blocker, id);
        return value;
    }

    private static void ValidateDotnetAuthorities(Arch7bFileAuthority root, Arch7bFileAuthority executable)
    {
        if (!root.MustExist || !executable.MustExist || !Path.IsPathFullyQualified(root.Path) ||
            !Path.IsPathFullyQualified(executable.Path) || !Directory.Exists(root.Path) ||
            !File.Exists(executable.Path) || IsReparsePoint(root.Path) || IsReparsePoint(executable.Path) ||
            !string.Equals(Path.GetFullPath(executable.Path), Path.Combine(Path.GetFullPath(root.Path), "dotnet.exe"),
                StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable.Path)));
        if (sha != executable.Sha256)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetExecutableShaMismatch);
    }

    private static bool ValidExecutableSearchPath(string value, string? executablePath)
    {
        var directories = value.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (directories.Length is < 1 or > 2 || directories.Distinct(
                StringComparer.OrdinalIgnoreCase).Count() != directories.Length || directories.Any(directory =>
                !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory) ||
                IsReparsePoint(directory))) return false;
        if (executablePath is null) return true;
        var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        return executableDirectory is not null && directories.Contains(executableDirectory,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateExecutableAuthority(Arch7bFileAuthority executable,
        string pathBlocker, string shaBlocker)
    {
        if (!executable.MustExist || !Path.IsPathFullyQualified(executable.Path) ||
            !File.Exists(executable.Path) || IsReparsePoint(executable.Path))
            throw new Arch7bQualificationException(pathBlocker);
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable.Path));
        if (directory is null || !Directory.Exists(directory) || IsReparsePoint(directory))
            throw new Arch7bQualificationException(pathBlocker);
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable.Path)));
        if (sha != executable.Sha256)
            throw new Arch7bQualificationException(shaBlocker);
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}