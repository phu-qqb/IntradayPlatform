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

    public static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ValidateTemplate(
        IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        if (values.Count == 0) return values;
        if (values.Count != 1 || values[0].VariableName != "DOTNET_ROOT" ||
            values[0].ValueKind != Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory ||
            values[0].SourceAuthorityId != "dotnet_root")
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
        var expected = ForDotnetRoot(authorities).Single();
        if (values[0] != expected)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        return values;
    }

    public static void ValidateMaterialized(IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values)
    {
        foreach (var value in values)
        {
            if (value.ContractVersion != Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion ||
                value.VariableName != "DOTNET_ROOT" ||
                value.ValueKind != Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory ||
                !Path.IsPathFullyQualified(value.Value) || !Arch7bOneShotContracts.IsSha256(value.SourceAuthoritySha256))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden);
            var canonical = string.Join('\n', value.ContractVersion, value.VariableName, value.ValueKind,
                value.Value, value.SourceAuthorityId, value.SourceAuthoritySha256);
            if (value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(canonical))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch);
        }
        if (values.Count > 1 || values.Select(value => value.VariableName).Distinct(StringComparer.Ordinal).Count() != values.Count)
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

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}