using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bNonSecretEnvironmentValueKind
{
    AbsoluteDirectory
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

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ForGitExecutablePath(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var executable = Require(authorities, "git_executable",
            Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
        ValidateGitExecutableAuthority(executable);
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable.Path)) ??
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        var canonical = string.Join('\n', Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            "PATH", Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory, directory,
            executable.AuthorityId, executable.Sha256);
        return [new(Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion, "PATH",
            Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory, directory, executable.AuthorityId,
            executable.Sha256, Arch7bOneShotContracts.Sha256(canonical))];
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
                "PATH" => (ForGitExecutablePath(authorities).Single(),
                    Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch),
                _ => throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden)
            };
            if (value != expected)
                throw new Arch7bQualificationException(blocker);
        }
        return values;
    }

    public static void ValidateMaterialized(IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values)
    {
        foreach (var value in values)
        {
            var expectedSource = value.VariableName switch
            {
                "DOTNET_ROOT" => "dotnet_root",
                "PATH" => "git_executable",
                _ => null
            };
            if (value.ContractVersion != Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion ||
                expectedSource is null || value.SourceAuthorityId != expectedSource ||
                value.ValueKind != Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory ||
                !Path.IsPathFullyQualified(value.Value) || !Arch7bOneShotContracts.IsSha256(value.SourceAuthoritySha256))
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

    private static void ValidateGitExecutableAuthority(Arch7bFileAuthority executable)
    {
        if (!executable.MustExist || !Path.IsPathFullyQualified(executable.Path) ||
            !File.Exists(executable.Path) || IsReparsePoint(executable.Path))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable.Path));
        if (directory is null || !Directory.Exists(directory) || IsReparsePoint(directory))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutablePathAuthorityMismatch);
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable.Path)));
        if (sha != executable.Sha256)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandGitExecutableShaMismatch);
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}