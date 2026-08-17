using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bLiveCliAuthorityBindingKind
{
    DirectoryAuthority,
    FileContent
}

public sealed record Arch7bLiveCliAuthorityBinding(
    string AuthorityId,
    Arch7bLiveCliAuthorityBindingKind AuthorityKind,
    string CliPath,
    string TargetAuthorityPath,
    string TargetAuthoritySha256,
    string ShaSemantics,
    bool PathMatched,
    bool ContentShaMatched,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', AuthorityId, AuthorityKind, CliPath,
        TargetAuthorityPath, TargetAuthoritySha256, ShaSemantics, PathMatched,
        ContentShaMatched);
}

public sealed record Arch7bLiveCliAuthorityBindingValidation(
    string ContractVersion,
    int RequiredBindingCount,
    int ValidatedBindingCount,
    int PathMismatchCount,
    int FileContentShaMismatchCount,
    IReadOnlyList<Arch7bLiveCliAuthorityBinding> Bindings,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, RequiredBindingCount,
        ValidatedBindingCount, PathMismatchCount, FileContentShaMismatchCount,
        string.Join('|', Bindings.Select(value => value.EvidenceSha256)));
}

public static class Arch7bLiveCliAuthorityBindingValidator
{
    public static readonly IReadOnlyDictionary<string, Arch7bLiveCliAuthorityBindingKind>
        RequiredBindings = new Dictionary<string, Arch7bLiveCliAuthorityBindingKind>(
            StringComparer.Ordinal)
        {
            ["core_repository"] = Arch7bLiveCliAuthorityBindingKind.DirectoryAuthority,
            ["intraday_runtime"] = Arch7bLiveCliAuthorityBindingKind.DirectoryAuthority,
            ["git_executable"] = Arch7bLiveCliAuthorityBindingKind.FileContent,
            ["root_certificate"] = Arch7bLiveCliAuthorityBindingKind.FileContent
        };

    public static Arch7bLiveCliAuthorityBindingValidation Validate(
        Arch7bOneShotLivePlanTemplate template,
        IReadOnlyDictionary<string, string> cliPaths)
    {
        var bindings = new List<Arch7bLiveCliAuthorityBinding>();
        foreach (var requirement in RequiredBindings.OrderBy(value => value.Key,
                     StringComparer.Ordinal))
        {
            var authorityId = requirement.Key;
            if (!template.FileAuthorities.TryGetValue(authorityId, out var authority) ||
                !cliPaths.TryGetValue(authorityId, out var cliPath) ||
                string.IsNullOrWhiteSpace(cliPath))
                throw Failure(Arch7bV2Blockers.LiveCliAuthorityMissing, authorityId);

            var path = Path.GetFullPath(cliPath);
            var targetPath = Path.GetFullPath(authority.Path);
            if (!string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase))
                throw Failure(Arch7bV2Blockers.LiveCliAuthorityPathMismatch,
                    authorityId + ":path");

            var contentMatched = true;
            var semantics = requirement.Value switch
            {
                Arch7bLiveCliAuthorityBindingKind.DirectoryAuthority =>
                    "TARGET_DIRECTORY_INVENTORY_SHA256_VALIDATED_BY_STATIC_AUTHORITY",
                Arch7bLiveCliAuthorityBindingKind.FileContent => "FILE_CONTENT_SHA256",
                _ => throw Failure(Arch7bV2Blockers.LiveCliAuthorityKindMismatch, authorityId)
            };
            switch (requirement.Value)
            {
                case Arch7bLiveCliAuthorityBindingKind.DirectoryAuthority:
                    if (!Directory.Exists(path) || File.Exists(path))
                        throw Failure(Arch7bV2Blockers.LiveCliAuthorityKindMismatch,
                            authorityId + ":directory");
                    break;
                case Arch7bLiveCliAuthorityBindingKind.FileContent:
                    if (!File.Exists(path) || Directory.Exists(path))
                        throw Failure(Arch7bV2Blockers.LiveCliAuthorityKindMismatch,
                            authorityId + ":file");
                    contentMatched = FileSha(path) == authority.Sha256;
                    if (!contentMatched)
                        throw Failure(Arch7bV2Blockers.LiveCliAuthorityFileContentShaMismatch,
                            authorityId + ":file-content");
                    break;
            }

            var provisional = new Arch7bLiveCliAuthorityBinding(authorityId,
                requirement.Value, path, targetPath, authority.Sha256, semantics,
                true, contentMatched, string.Empty);
            bindings.Add(provisional with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
            });
        }

        var value = new Arch7bLiveCliAuthorityBindingValidation(
            Arch7bV2Contracts.LiveCliAuthorityBindingValidationVersion,
            RequiredBindings.Count, bindings.Count, 0, 0, bindings, string.Empty);
        return value with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical())
        };
    }

    private static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static Arch7bQualificationException Failure(string blocker, string detail) =>
        new(blocker, detail);
}
