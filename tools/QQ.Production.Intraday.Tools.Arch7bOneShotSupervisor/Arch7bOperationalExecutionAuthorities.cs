using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bOperationalAuthorityKind
{
    File,
    DirectoryInventory,
    GitRepository,
    NodePackageRuntime,
    DotnetRuntime,
    RootCa,
    StaticConfig
}

public enum Arch7bOperationalAuthorityReferenceKind
{
    Executable,
    WorkingDirectory,
    PlaceholderPath,
    PlaceholderSha256,
    NonSecretEnvironment,
    StaticPreSpawn
}

public sealed record Arch7bOperationalAuthorityReference(
    string ContractVersion,
    string AuthorityId,
    Arch7bOperationalAuthorityReferenceKind ReferenceKind,
    string ReferencingStageId,
    string ReferencingCommandId,
    string SourceField,
    Arch7bOperationalAuthorityKind ExpectedAuthorityKind,
    bool Required,
    bool MustExist,
    bool MustBeInsideRunRoot,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, AuthorityId, ReferenceKind,
        ReferencingStageId, ReferencingCommandId, SourceField, ExpectedAuthorityKind, Required,
        MustExist, MustBeInsideRunRoot);
}

public sealed record Arch7bRequiredOperationalExecutionAuthorityInventory(
    string ContractVersion,
    int StageCount,
    int CommandTemplateCount,
    int AuthorityReferenceCount,
    int RequiredAuthorityIdCount,
    int DuplicateConflictingReferenceCount,
    int UnknownAuthorityKindCount,
    int UnresolvedReferenceCount,
    IReadOnlyList<Arch7bOperationalAuthorityReference> References,
    string EvidenceSha256)
{
    public IReadOnlySet<string> RequiredAuthorityIds => References.Where(value => value.Required)
        .Select(value => value.AuthorityId).ToHashSet(StringComparer.Ordinal);

    public string Canonical() => string.Join('\n', ContractVersion, StageCount, CommandTemplateCount,
        AuthorityReferenceCount, RequiredAuthorityIdCount, DuplicateConflictingReferenceCount,
        UnknownAuthorityKindCount, UnresolvedReferenceCount,
        string.Join('|', References.Select(value => value.EvidenceSha256)));

    public void ValidateEvidence()
    {
        if (ContractVersion != Arch7bV2Contracts.OperationalExecutionAuthorityInventoryVersion ||
            StageCount != Arch7bStages.All.Count ||
            CommandTemplateCount != Arch7bFinalStageExecutionCatalog.CommandTemplateCount ||
            AuthorityReferenceCount != References.Count ||
            RequiredAuthorityIdCount != RequiredAuthorityIds.Count ||
            DuplicateConflictingReferenceCount != 0 || UnknownAuthorityKindCount != 0 ||
            UnresolvedReferenceCount != 0 || References.Any(value =>
                value.ContractVersion != Arch7bV2Contracts.OperationalExecutionAuthorityReferenceVersion ||
                value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(value.Canonical())) ||
            EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, "required-inventory");
    }

    private static Arch7bQualificationException Failure(string blocker, string detail) =>
        new(blocker, detail);
}

public sealed record Arch7bOperationalDirectoryInventoryEntry(
    string RelativePath,
    string EntryType,
    long ByteLength,
    string? FileSha256,
    bool Executable,
    bool ReparsePoint,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', RelativePath, EntryType, ByteLength,
        FileSha256 ?? string.Empty, Executable, ReparsePoint);
}

public sealed record Arch7bOperationalDirectoryInventory(
    string ContractVersion,
    string AuthorityId,
    string AbsolutePath,
    int FileCount,
    int DirectoryCount,
    int MissingCount,
    int UnexpectedCount,
    int DuplicateRelativePathCount,
    int ReparsePointCount,
    IReadOnlyList<Arch7bOperationalDirectoryInventoryEntry> Entries,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, AuthorityId, AbsolutePath,
        FileCount, DirectoryCount, MissingCount, UnexpectedCount, DuplicateRelativePathCount,
        ReparsePointCount, string.Join('|', Entries.Select(value => value.EvidenceSha256)));

    public void ValidateEvidence()
    {
        if (ContractVersion != Arch7bV2Contracts.OperationalExecutionAuthorityDirectoryInventoryVersion ||
            FileCount != Entries.Count(value => value.EntryType == "FILE") ||
            DirectoryCount != Entries.Count(value => value.EntryType == "DIRECTORY") ||
            MissingCount != 0 || UnexpectedCount != 0 || DuplicateRelativePathCount != 0 ||
            ReparsePointCount != 0 || Entries.Any(value => value.ReparsePoint ||
                value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(value.Canonical())) ||
            EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw new Arch7bQualificationException(
                Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch, AuthorityId);
    }
}

public sealed record Arch7bOperationalExecutionAuthority(
    string ContractVersion,
    string AuthorityId,
    Arch7bOperationalAuthorityKind AuthorityKind,
    string Path,
    string? FileSha256,
    string? DirectoryInventorySha256,
    string? InventoryManifestPath,
    string? InventoryManifestSha256,
    string? Repository,
    string? Commit,
    string? Tree,
    string? PackageJsonSha256,
    string? PackageLockSha256,
    string? RuntimeClosureSha256,
    string? RuntimeVersion,
    bool MustExist,
    bool MustBeInsideRunRoot,
    string ProducerAuthorityId,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, AuthorityId, AuthorityKind, Path,
        FileSha256 ?? string.Empty, DirectoryInventorySha256 ?? string.Empty,
        InventoryManifestPath ?? string.Empty, InventoryManifestSha256 ?? string.Empty,
        Repository ?? string.Empty, Commit ?? string.Empty, Tree ?? string.Empty,
        PackageJsonSha256 ?? string.Empty, PackageLockSha256 ?? string.Empty,
        RuntimeClosureSha256 ?? string.Empty, RuntimeVersion ?? string.Empty,
        MustExist, MustBeInsideRunRoot, ProducerAuthorityId);

    public Arch7bFileAuthority Project() => new(AuthorityId, Path,
        AuthorityKind is Arch7bOperationalAuthorityKind.File or
            Arch7bOperationalAuthorityKind.RootCa or Arch7bOperationalAuthorityKind.StaticConfig
            ? FileSha256 ?? throw Mismatch(AuthorityId)
            : DirectoryInventorySha256 ?? throw Mismatch(AuthorityId),
        MustExist, MustBeInsideRunRoot);

    private static Arch7bQualificationException Mismatch(string detail) =>
        new(Arch7bV2Contracts.OperationalAuthorityShaMismatch, detail);
}

public sealed record Arch7bOperationalExecutionAuthorityManifest(
    string ContractVersion,
    string SourceTemplateSha256,
    string RequiredAuthorityInventorySha256,
    int AuthorityCount,
    IReadOnlyList<Arch7bOperationalExecutionAuthority> Authorities,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, SourceTemplateSha256,
        RequiredAuthorityInventorySha256, AuthorityCount,
        string.Join('|', Authorities.OrderBy(value => value.AuthorityId, StringComparer.Ordinal)
            .Select(value => value.EvidenceSha256)));

    public IReadOnlyDictionary<string, Arch7bFileAuthority> Project(
        Arch7bRequiredOperationalExecutionAuthorityInventory inventory)
    {
        inventory.ValidateEvidence();
        ValidateEvidence();
        if (RequiredAuthorityInventorySha256 != inventory.EvidenceSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, "inventory");
        if (Authorities.GroupBy(value => value.AuthorityId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDuplicateId, "manifest-array");
        var manifestIds = Authorities.Select(value => value.AuthorityId)
            .ToHashSet(StringComparer.Ordinal);
        var missing = inventory.RequiredAuthorityIds.Except(manifestIds,
            StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, missing);
        var unused = manifestIds.Except(inventory.RequiredAuthorityIds,
            StringComparer.Ordinal).FirstOrDefault();
        if (unused is not null)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityUnused, unused);
        var kinds = inventory.References.GroupBy(value => value.AuthorityId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(value => value.ExpectedAuthorityKind)
                .Distinct().Single(), StringComparer.Ordinal);
        foreach (var authority in Authorities)
        {
            if (authority.AuthorityKind != kinds[authority.AuthorityId])
                throw Failure(Arch7bV2Contracts.OperationalAuthorityKindMismatch, authority.AuthorityId);
            var references = inventory.References.Where(value =>
                value.AuthorityId == authority.AuthorityId).ToArray();
            if (authority.MustExist != references.Any(value => value.MustExist) ||
                authority.MustBeInsideRunRoot != references.Any(value => value.MustBeInsideRunRoot))
                throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, authority.AuthorityId);
        }
        return Authorities.OrderBy(value => value.AuthorityId, StringComparer.Ordinal)
            .ToDictionary(value => value.AuthorityId, value => value.Project(), StringComparer.Ordinal);
    }

    public void ValidateEvidence()
    {
        if (ContractVersion != Arch7bV2Contracts.OperationalExecutionAuthorityManifestVersion ||
            AuthorityCount != Authorities.Count || Authorities.Any(value =>
                value.ContractVersion != Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion ||
                value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(value.Canonical())) ||
            EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, "manifest-evidence");
    }

    private static Arch7bQualificationException Failure(string blocker, string detail) =>
        new(blocker, detail);
}

public static class Arch7bOperationalExecutionAuthorityManifestParser
{
    public static Arch7bOperationalExecutionAuthorityManifest ParseStrict(ReadOnlySpan<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes.ToArray());
        RejectDuplicateProperties(document.RootElement);
        var manifest = JsonSerializer.Deserialize<Arch7bOperationalExecutionAuthorityManifest>(
            bytes, Arch7bJson.CanonicalOptions) ?? throw Duplicate("manifest-null");
        if (manifest.Authorities.GroupBy(value => value.AuthorityId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1)) throw Duplicate("authority-id");
        manifest.ValidateEvidence();
        return manifest;
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Duplicate(property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }

    private static Arch7bQualificationException Duplicate(string detail) =>
        new(Arch7bV2Contracts.OperationalAuthorityDuplicateId, detail);
}

public static class Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder
{
    private static readonly IReadOnlyDictionary<string, Arch7bOperationalAuthorityKind>
        KnownAuthorities = new Dictionary<string, Arch7bOperationalAuthorityKind>(StringComparer.Ordinal)
        {
            ["core_repository"] = Arch7bOperationalAuthorityKind.GitRepository,
            ["core_node_runtime"] = Arch7bOperationalAuthorityKind.NodePackageRuntime,
            ["intraday_runtime"] = Arch7bOperationalAuthorityKind.DirectoryInventory,
            ["git_executable"] = Arch7bOperationalAuthorityKind.File,
            ["node_executable"] = Arch7bOperationalAuthorityKind.File,
            ["taskkill_executable"] = Arch7bOperationalAuthorityKind.File,
            ["chrome_executable"] = Arch7bOperationalAuthorityKind.File,
            ["dotnet_executable"] = Arch7bOperationalAuthorityKind.File,
            ["dotnet_root"] = Arch7bOperationalAuthorityKind.DotnetRuntime,
            ["root_certificate"] = Arch7bOperationalAuthorityKind.RootCa,
            ["market_data_config"] = Arch7bOperationalAuthorityKind.StaticConfig
        };

    private static readonly IReadOnlyDictionary<string, Arch7bOperationalAuthorityKind>
        StaticPreSpawnAuthorities = new Dictionary<string, Arch7bOperationalAuthorityKind>(StringComparer.Ordinal)
        {
            ["core_repository"] = Arch7bOperationalAuthorityKind.GitRepository,
            ["core_node_runtime"] = Arch7bOperationalAuthorityKind.NodePackageRuntime,
            ["intraday_runtime"] = Arch7bOperationalAuthorityKind.DirectoryInventory,
            ["git_executable"] = Arch7bOperationalAuthorityKind.File,
            ["node_executable"] = Arch7bOperationalAuthorityKind.File,
            ["chrome_executable"] = Arch7bOperationalAuthorityKind.File,
            ["dotnet_executable"] = Arch7bOperationalAuthorityKind.File,
            ["dotnet_root"] = Arch7bOperationalAuthorityKind.DotnetRuntime,
            ["root_certificate"] = Arch7bOperationalAuthorityKind.RootCa,
            ["market_data_config"] = Arch7bOperationalAuthorityKind.StaticConfig
        };

    public static Arch7bRequiredOperationalExecutionAuthorityInventory Build(
        Arch7bOneShotLivePlanTemplate template)
    {
        var references = new List<Arch7bOperationalAuthorityReference>();
        foreach (var command in template.CommandTemplates)
        {
            Add(command.ExecutableAuthorityId, Arch7bOperationalAuthorityReferenceKind.Executable,
                command.CommandId, command.StageId, "executable_authority_id", false);
            Add(command.WorkingDirectoryAuthorityId,
                Arch7bOperationalAuthorityReferenceKind.WorkingDirectory,
                command.CommandId, command.StageId, "working_directory_authority_id", false);
            foreach (var argument in command.ArgumentTemplates)
            {
                var parsed = Arch7bTypedPlaceholder.Parse(argument.Value);
                if (parsed is not { Scope: "authority" } placeholder) continue;
                var kind = placeholder.Field == "sha256"
                    ? Arch7bOperationalAuthorityReferenceKind.PlaceholderSha256
                    : Arch7bOperationalAuthorityReferenceKind.PlaceholderPath;
                Add(placeholder.Name, kind, command.CommandId, command.StageId,
                    "argument_templates:" + argument.Value, argument.MustBeInsideRunRoot);
            }
            foreach (var environment in command.NonSecretEnvironment)
            {
                if (command.StageId == "CORE_PREQUALIFICATION" &&
                    environment.VariableName == "PATH" &&
                    environment.SourceAuthorityId ==
                    Arch7bSealedNonSecretEnvironment.CorePrequalificationPathAuthorityId)
                {
                    foreach (var sourceId in Arch7bSealedNonSecretEnvironment
                                 .CorePrequalificationPathSourceAuthorityIds)
                        Add(sourceId, Arch7bOperationalAuthorityReferenceKind.NonSecretEnvironment,
                            command.CommandId, command.StageId,
                            "non_secret_environment:PATH:" + sourceId, false);
                }
                else
                {
                    Add(environment.SourceAuthorityId,
                        Arch7bOperationalAuthorityReferenceKind.NonSecretEnvironment,
                        command.CommandId, command.StageId,
                        "non_secret_environment:" + environment.VariableName, false);
                }
            }
        }
        foreach (var pair in StaticPreSpawnAuthorities)
            Add(pair.Key, Arch7bOperationalAuthorityReferenceKind.StaticPreSpawn,
                "run-one-shot", "STATIC_AUTHORITY_VALIDATION", "pre_spawn_cli_binding", false);
        var conflicting = references.GroupBy(value => value.AuthorityId, StringComparer.Ordinal)
            .Count(group => group.Select(value => value.ExpectedAuthorityKind).Distinct().Count() > 1);
        var ordered = references.OrderBy(value => value.AuthorityId, StringComparer.Ordinal)
            .ThenBy(value => value.ReferencingCommandId, StringComparer.Ordinal)
            .ThenBy(value => value.SourceField, StringComparer.Ordinal).ToArray();
        var requiredCount = ordered.Where(value => value.Required).Select(value => value.AuthorityId)
            .Distinct(StringComparer.Ordinal).Count();
        var value = new Arch7bRequiredOperationalExecutionAuthorityInventory(
            Arch7bV2Contracts.OperationalExecutionAuthorityInventoryVersion,
            template.StageContracts.Count, template.CommandTemplates.Count, ordered.Length, requiredCount,
            conflicting, 0, 0, ordered, string.Empty);
        var result = value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
        result.ValidateEvidence();
        return result;

        void Add(string id, Arch7bOperationalAuthorityReferenceKind referenceKind,
            string commandId, string stageId, string source, bool insideRunRoot)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new Arch7bQualificationException(
                    Arch7bV2Contracts.OperationalAuthorityMissing, source);
            var item = new Arch7bOperationalAuthorityReference(
                Arch7bV2Contracts.OperationalExecutionAuthorityReferenceVersion, id, referenceKind,
                stageId, commandId, source, Infer(id, referenceKind), true, true, insideRunRoot,
                string.Empty);
            references.Add(item with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(item.Canonical())
            });
        }
    }

    private static Arch7bOperationalAuthorityKind Infer(string id,
        Arch7bOperationalAuthorityReferenceKind referenceKind)
    {
        if (KnownAuthorities.TryGetValue(id, out var known)) return known;
        if (id.EndsWith("_config", StringComparison.Ordinal))
            return Arch7bOperationalAuthorityKind.StaticConfig;
        if (id.EndsWith("_repository", StringComparison.Ordinal))
            return Arch7bOperationalAuthorityKind.GitRepository;
        if (id.EndsWith("_working_directory", StringComparison.Ordinal) ||
            id.EndsWith("_runtime", StringComparison.Ordinal) ||
            referenceKind is Arch7bOperationalAuthorityReferenceKind.WorkingDirectory or
                Arch7bOperationalAuthorityReferenceKind.NonSecretEnvironment)
            return Arch7bOperationalAuthorityKind.DirectoryInventory;
        return Arch7bOperationalAuthorityKind.File;
    }
}

public sealed record Arch7bOperationalAuthorityValidationItem(
    string AuthorityId, Arch7bOperationalAuthorityKind AuthorityKind, bool Passed,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', AuthorityId, AuthorityKind, Passed);
}

public sealed record Arch7bOperationalExecutionAuthorityValidation(
    string ContractVersion,
    int RequiredAuthorityCount,
    int ValidatedAuthorityCount,
    int MissingAuthorityCount,
    int UnusedAuthorityCount,
    int DuplicateAuthorityCount,
    int FileShaMismatchCount,
    int DirectoryInventoryMismatchCount,
    IReadOnlyList<Arch7bOperationalAuthorityValidationItem> Authorities,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, RequiredAuthorityCount,
        ValidatedAuthorityCount, MissingAuthorityCount, UnusedAuthorityCount,
        DuplicateAuthorityCount, FileShaMismatchCount, DirectoryInventoryMismatchCount,
        string.Join('|', Authorities.Select(value => value.EvidenceSha256)));
}

public static class Arch7bOperationalExecutionAuthorityValidator
{
    public const string ValidationFileName =
        "arch7b-operational-execution-authority-validation-v1.json";

    public static Arch7bOperationalExecutionAuthorityValidation ValidateStatic(
        Arch7bRequiredOperationalExecutionAuthorityInventory inventory,
        Arch7bOperationalExecutionAuthorityManifest manifest,
        IReadOnlyDictionary<string, Arch7bFileAuthority>? templateAuthorities = null,
        IReadOnlyDictionary<string, Arch7bFileAuthority>? liveAuthorities = null,
        string? evidencePath = null)
    {
        var projected = manifest.Project(inventory);
        if (templateAuthorities is not null) RequireExactProjection(projected, templateAuthorities,
            "template");
        if (liveAuthorities is not null) RequireExactProjection(projected, liveAuthorities, "live");
        var byId = manifest.Authorities.ToDictionary(value => value.AuthorityId,
            StringComparer.Ordinal);
        var gitExecutable = byId.TryGetValue("git_executable", out var git) ? git.Path : null;
        var nodeExecutable = byId.TryGetValue("node_executable", out var node) ? node.Path : null;
        if (byId.TryGetValue("dotnet_root", out var dotnetRoot) &&
            byId.TryGetValue("dotnet_executable", out var dotnetExecutable))
        {
            var expectedDotnet = System.IO.Path.Combine(dotnetRoot.Path,
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (!SamePath(expectedDotnet, dotnetExecutable.Path))
                throw Failure(Arch7bV2Contracts.OperationalAuthorityDotnetMismatch,
                    "dotnet-executable-root-binding");
        }
        var items = new List<Arch7bOperationalAuthorityValidationItem>();
        foreach (var source in manifest.Authorities.OrderBy(value => value.AuthorityId,
                     StringComparer.Ordinal))
        {
            ValidatePath(source);
            switch (source.AuthorityKind)
            {
                case Arch7bOperationalAuthorityKind.File:
                case Arch7bOperationalAuthorityKind.RootCa:
                case Arch7bOperationalAuthorityKind.StaticConfig:
                    ValidateFile(source);
                    if (source.AuthorityId == "taskkill_executable")
                        Arch7bSealedNonSecretEnvironment.ValidateTaskkillAuthority(source.Project());
                    if (source.AuthorityId == "chrome_executable")
                        Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(source.Project());
                    break;
                case Arch7bOperationalAuthorityKind.DirectoryInventory:
                    ValidateDirectory(source);
                    break;
                case Arch7bOperationalAuthorityKind.GitRepository:
                    ValidateDirectory(source);
                    ValidateGitRepository(source, gitExecutable);
                    break;
                case Arch7bOperationalAuthorityKind.NodePackageRuntime:
                    ValidateDirectory(source);
                    ValidateNodePackage(source, nodeExecutable);
                    break;
                case Arch7bOperationalAuthorityKind.DotnetRuntime:
                    ValidateDirectory(source);
                    ValidateDotnetRuntime(source);
                    break;
                default:
                    throw Failure(Arch7bV2Contracts.OperationalAuthorityKindMismatch,
                        source.AuthorityId);
            }
            var item = new Arch7bOperationalAuthorityValidationItem(source.AuthorityId,
                source.AuthorityKind, true, string.Empty);
            items.Add(item with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(item.Canonical()) });
        }
        var value = new Arch7bOperationalExecutionAuthorityValidation(
            Arch7bV2Contracts.OperationalExecutionAuthorityValidationVersion,
            inventory.RequiredAuthorityIdCount, items.Count, 0, 0, 0, 0, 0, items,
            string.Empty);
        var result = value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
        if (evidencePath is not null)
        {
            evidencePath = System.IO.Path.GetFullPath(evidencePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(evidencePath)!);
            File.WriteAllBytes(evidencePath,
                JsonSerializer.SerializeToUtf8Bytes(result, Arch7bJson.CanonicalOptions));
        }
        return result;
    }

    public static Arch7bOperationalDirectoryInventory DirectoryInventory(string id, string root)
    {
        root = System.IO.Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, id);
        var entries = new List<Arch7bOperationalDirectoryInventoryEntry>();
        Visit(new DirectoryInfo(root), string.Empty);
        var ordered = entries.OrderBy(value => value.RelativePath, StringComparer.Ordinal).ToArray();
        var value = new Arch7bOperationalDirectoryInventory(
            Arch7bV2Contracts.OperationalExecutionAuthorityDirectoryInventoryVersion, id, root,
            ordered.Count(item => item.EntryType == "FILE"),
            ordered.Count(item => item.EntryType == "DIRECTORY"), 0, 0,
            ordered.GroupBy(item => item.RelativePath, StringComparer.Ordinal)
                .Count(group => group.Count() > 1),
            ordered.Count(item => item.ReparsePoint), ordered, string.Empty);
        var result = value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
        result.ValidateEvidence();
        return result;

        void Visit(DirectoryInfo directory, string relativeRoot)
        {
            foreach (var info in directory.EnumerateFileSystemInfos()
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                if (relativeRoot.Length == 0 && info.Name == ".git") continue;
                var relative = string.IsNullOrEmpty(relativeRoot)
                    ? info.Name : relativeRoot + "/" + info.Name;
                relative = relative.Replace('\\', '/');
                var reparse = (info.Attributes & FileAttributes.ReparsePoint) != 0;
                if (reparse)
                    throw Failure(Arch7bV2Contracts.OperationalAuthorityReparsePoint, id);
                if (info is DirectoryInfo child)
                {
                    Add(relative, "DIRECTORY", 0, null, false, false);
                    Visit(child, relative);
                }
                else
                {
                    var file = (FileInfo)info;
                    Add(relative, "FILE", file.Length, FileSha(file.FullName),
                        IsExecutable(file.FullName), false);
                }
            }
        }

        void Add(string relative, string entryType, long length, string? sha,
            bool executable, bool reparse)
        {
            var entry = new Arch7bOperationalDirectoryInventoryEntry(relative, entryType,
                length, sha, executable, reparse, string.Empty);
            entries.Add(entry with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(entry.Canonical())
            });
        }
    }

    internal static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    internal static void RequireExactProjection(
        IReadOnlyDictionary<string, Arch7bFileAuthority> expected,
        IReadOnlyDictionary<string, Arch7bFileAuthority> actual,
        string source)
    {
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(actual.Keys.ToHashSet(StringComparer.Ordinal)))
            throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, source);
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var observed))
                throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, pair.Key);
            if (!string.Equals(pair.Value.Path, observed.Path, StringComparison.OrdinalIgnoreCase))
                throw Failure(Arch7bV2Contracts.OperationalAuthorityPathMismatch, pair.Key);
            if (pair.Value.Sha256 != observed.Sha256)
                throw Failure(Arch7bV2Contracts.OperationalAuthorityShaMismatch, pair.Key);
            if (pair.Value.AuthorityId != observed.AuthorityId ||
                pair.Value.MustExist != observed.MustExist ||
                pair.Value.MustBeInsideRunRoot != observed.MustBeInsideRunRoot)
                throw Failure(Arch7bV2Contracts.OperationalAuthoritySetMismatch, pair.Key);
        }
    }

    private static void ValidatePath(Arch7bOperationalExecutionAuthority authority)
    {
        if (!System.IO.Path.IsPathFullyQualified(authority.Path) ||
            authority.MustExist && !File.Exists(authority.Path) && !Directory.Exists(authority.Path))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, authority.AuthorityId);
        if (IsReparse(authority.Path))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityReparsePoint, authority.AuthorityId);
    }

    private static void ValidateFile(Arch7bOperationalExecutionAuthority authority)
    {
        if (!File.Exists(authority.Path) || Directory.Exists(authority.Path))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, authority.AuthorityId);
        if (!Arch7bOneShotContracts.IsSha256(authority.FileSha256 ?? string.Empty) ||
            FileSha(authority.Path) != authority.FileSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityShaMismatch, authority.AuthorityId);
    }

    private static void ValidateDirectory(Arch7bOperationalExecutionAuthority authority)
    {
        if (!Directory.Exists(authority.Path) || authority.InventoryManifestPath is null ||
            authority.InventoryManifestSha256 is null ||
            !File.Exists(authority.InventoryManifestPath) ||
            FileSha(authority.InventoryManifestPath) != authority.InventoryManifestSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
                authority.AuthorityId);
        var stored = JsonSerializer.Deserialize<Arch7bOperationalDirectoryInventory>(
            File.ReadAllBytes(authority.InventoryManifestPath), Arch7bJson.CanonicalOptions) ??
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
                authority.AuthorityId);
        stored.ValidateEvidence();
        if (!string.Equals(stored.AuthorityId, authority.AuthorityId, StringComparison.Ordinal) ||
            !string.Equals(System.IO.Path.GetFullPath(stored.AbsolutePath),
                System.IO.Path.GetFullPath(authority.Path), StringComparison.OrdinalIgnoreCase) ||
            stored.EvidenceSha256 != authority.DirectoryInventorySha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
                authority.AuthorityId);
        var actual = DirectoryInventory(authority.AuthorityId, authority.Path);
        if (actual.EvidenceSha256 != stored.EvidenceSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
                authority.AuthorityId);
    }

    private static void ValidateGitRepository(Arch7bOperationalExecutionAuthority authority,
        string? gitExecutable)
    {
        if (gitExecutable is null || !File.Exists(gitExecutable))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, "git_executable");
        if (!IsSha1(authority.Commit) || !IsSha1(authority.Tree) ||
            string.IsNullOrWhiteSpace(authority.Repository))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityGitMismatch, authority.AuthorityId);
        var head = Run(gitExecutable, authority.Path, "rev-parse", "HEAD");
        var tree = Run(gitExecutable, authority.Path, "rev-parse", "HEAD^{tree}");
        var remote = Run(gitExecutable, authority.Path, "remote", "get-url", "origin");
        var status = Run(gitExecutable, authority.Path, "status", "--porcelain=v1",
            "--untracked-files=all");
        var shallow = Run(gitExecutable, authority.Path, "rev-parse", "--is-shallow-repository");
        Run(gitExecutable, authority.Path, "fsck", "--full", "--strict");
        var alternates = System.IO.Path.Combine(authority.Path, ".git", "objects", "info", "alternates");
        if (head != authority.Commit || tree != authority.Tree || status.Length != 0 ||
            shallow != "false" || !SameRepository(remote, authority.Repository) ||
            File.Exists(alternates) && new FileInfo(alternates).Length != 0 ||
            ContainsReparse(authority.Path))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityGitMismatch, authority.AuthorityId);
    }

    private static void ValidateNodePackage(Arch7bOperationalExecutionAuthority authority,
        string? nodeExecutable)
    {
        if (nodeExecutable is null || !File.Exists(nodeExecutable))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMissing, "node_executable");
        var packageRoot = authority.AuthorityKind == Arch7bOperationalAuthorityKind.GitRepository
            ? System.IO.Path.Combine(authority.Path, "tools", "lmax_portal_reports_downloader")
            : authority.Path;
        var packageJson = System.IO.Path.Combine(packageRoot, "package.json");
        var packageLock = System.IO.Path.Combine(packageRoot, "package-lock.json");
        var modules = System.IO.Path.Combine(packageRoot, "node_modules");
        if (string.IsNullOrWhiteSpace(authority.RuntimeVersion) ||
            Run(nodeExecutable, packageRoot, "--version") != authority.RuntimeVersion)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityNodeMismatch, authority.AuthorityId);
        if (!File.Exists(packageJson) || !File.Exists(packageLock) || !Directory.Exists(modules) ||
            FileSha(packageJson) != authority.PackageJsonSha256 ||
            FileSha(packageLock) != authority.PackageLockSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityNodeMismatch, authority.AuthorityId);
        var closure = DirectoryInventory(authority.AuthorityId + "-node-runtime", packageRoot);
        if (closure.EvidenceSha256 != authority.RuntimeClosureSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityNodeMismatch, authority.AuthorityId);
        Run(nodeExecutable, packageRoot, "-e",
            "Promise.all([import('@aws-sdk/client-secrets-manager'),import('playwright')," +
            "import('./src/core-runtime-prequalification.mjs')," +
            "import('./src/rds-secret-child-command-broker-reference-client.mjs')])" +
            ".catch(error=>{console.error(error.message);process.exit(1)})");
        var npmCli = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(nodeExecutable)!,
            "node_modules", "npm", "bin", "npm-cli.js");
        if (!File.Exists(npmCli))
            throw Failure(Arch7bV2Contracts.OperationalAuthorityNodeMismatch, "npm-executable");
        Run(nodeExecutable, packageRoot, npmCli, "test");
        Run(nodeExecutable, packageRoot, npmCli, "audit", "--offline", "--omit=dev",
            "--audit-level=low");
        if (DirectoryInventory(authority.AuthorityId + "-node-runtime", packageRoot)
                .EvidenceSha256 != authority.RuntimeClosureSha256)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityNodeMismatch, authority.AuthorityId);
    }

    private static void ValidateDotnetRuntime(Arch7bOperationalExecutionAuthority authority)
    {
        var executable = System.IO.Path.Combine(authority.Path,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (!File.Exists(executable) || string.IsNullOrWhiteSpace(authority.RuntimeVersion) ||
            !Directory.Exists(System.IO.Path.Combine(authority.Path, "shared")) ||
            Run(executable, authority.Path, "--version") != authority.RuntimeVersion)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityDotnetMismatch,
                authority.AuthorityId);
    }

    private static string Run(string executable, string workingDirectory, params string[] arguments)
    {
        var start = CreateProcessStartInfo(executable, workingDirectory, arguments);
        using var process = Process.Start(start) ??
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMismatch, "process-start");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(true);
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMismatch, "process-timeout");
        }
        if (process.ExitCode != 0)
            throw Failure(Arch7bV2Contracts.OperationalAuthorityMismatch,
                "process-exit-" + process.ExitCode + "-" +
                Arch7bOneShotContracts.Sha256(output + "\n" + error));
        return output.Trim();
    }

    internal static ProcessStartInfo CreateProcessStartInfo(string executable,
        string workingDirectory, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var executableDirectory = System.IO.Path.GetDirectoryName(
            System.IO.Path.GetFullPath(executable))!;
        var inheritedPath = start.Environment.TryGetValue("PATH", out var pathValue)
            ? pathValue ?? string.Empty : string.Empty;
        start.Environment["PATH"] = executableDirectory + System.IO.Path.PathSeparator + inheritedPath;
        start.Environment["npm_config_offline"] = "true";
        return start;
    }

    private static bool IsExecutable(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
               extension.Length == 0;
    }

    private static bool ContainsReparse(string root)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        while (pending.Count != 0)
        {
            foreach (var info in pending.Pop().EnumerateFileSystemInfos())
            {
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return true;
                if (info is DirectoryInfo directory) pending.Push(directory);
            }
        }
        return false;
    }

    private static bool IsReparse(string path) => File.Exists(path) || Directory.Exists(path)
        ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
        : false;

    private static bool IsSha1(string? value) => value is { Length: 40 } &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static bool SamePath(string left, string right) => string.Equals(
        System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool SameRepository(string left, string right) => NormalizeRepository(left) ==
        NormalizeRepository(right);

    private static string NormalizeRepository(string value) => value.Trim().TrimEnd('/')
        .Replace("git@github.com:", "https://github.com/", StringComparison.OrdinalIgnoreCase)
        .Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

    private static Arch7bQualificationException Failure(string blocker, string detail) =>
        new(blocker, detail);
}

public sealed class Arch7bOperationalExecutionAuthorityMaterializer
{
    public async Task<object> MaterializeFilesAsync(string templatePath,
        string authorityPathMapPath, string outputRoot,
        CancellationToken cancellationToken = default)
    {
        var templateBytes = await File.ReadAllBytesAsync(System.IO.Path.GetFullPath(templatePath),
            cancellationToken).ConfigureAwait(false);
        var template = JsonSerializer.Deserialize<Arch7bOneShotLivePlanTemplate>(templateBytes,
            Arch7bJson.CanonicalOptions) ?? throw Mismatch("source-template");
        template.ValidateEvidence();
        var mapBytes = await File.ReadAllBytesAsync(System.IO.Path.GetFullPath(authorityPathMapPath),
            cancellationToken).ConfigureAwait(false);
        var paths = ParsePathMapStrict(mapBytes);
        var inventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder.Build(template);
        var manifest = await MaterializeAsync(template, paths, outputRoot, cancellationToken)
            .ConfigureAwait(false);
        var inventoryPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot),
            "arch7b-required-operational-execution-authority-inventory-v1.json");
        var manifestPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot),
            "arch7b-operational-execution-authority-manifest-v1.json");
        var inventoryBytes = JsonSerializer.SerializeToUtf8Bytes(inventory,
            Arch7bJson.CanonicalOptions);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest,
            Arch7bJson.CanonicalOptions);
        await File.WriteAllBytesAsync(inventoryPath, inventoryBytes, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(manifestPath, manifestBytes, cancellationToken)
            .ConfigureAwait(false);
        return new
        {
            verdict = "ARCH7B_OPERATIONAL_EXECUTION_AUTHORITY_MANIFEST_MATERIALIZED",
            stage_count = inventory.StageCount,
            command_template_count = inventory.CommandTemplateCount,
            authority_reference_count = inventory.AuthorityReferenceCount,
            required_authority_id_count = inventory.RequiredAuthorityIdCount,
            authority_count = manifest.AuthorityCount,
            inventory_path = inventoryPath,
            inventory_sha256 = Convert.ToHexStringLower(SHA256.HashData(inventoryBytes)),
            manifest_path = manifestPath,
            manifest_sha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes))
        };
    }

    public async Task<Arch7bOperationalExecutionAuthorityManifest> MaterializeAsync(
        Arch7bOneShotLivePlanTemplate template,
        IReadOnlyDictionary<string, string> authorityPaths,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        outputRoot = System.IO.Path.GetFullPath(outputRoot);
        if (Directory.Exists(outputRoot) && Directory.EnumerateFileSystemEntries(outputRoot).Any())
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootNotEmpty);
        Directory.CreateDirectory(outputRoot);
        var inventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder.Build(template);
        var missingIds = inventory.RequiredAuthorityIds.Except(authorityPaths.Keys,
            StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (missingIds.Length != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Contracts.OperationalAuthorityMissing, string.Join(',', missingIds));
        var unusedIds = authorityPaths.Keys.Except(inventory.RequiredAuthorityIds,
            StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (unusedIds.Length != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Contracts.OperationalAuthorityUnused, string.Join(',', unusedIds));
        var kinds = inventory.References.GroupBy(value => value.AuthorityId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ExpectedAuthorityKind,
                StringComparer.Ordinal);
        var entries = new List<Arch7bOperationalExecutionAuthority>();
        foreach (var pair in kinds.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!authorityPaths.TryGetValue(pair.Key, out var path))
                throw new Arch7bQualificationException(
                    Arch7bV2Contracts.OperationalAuthorityMissing, pair.Key);
            entries.Add(await MaterializeEntryAsync(pair.Key, pair.Value,
                System.IO.Path.GetFullPath(path), outputRoot, template, authorityPaths,
                inventory, cancellationToken).ConfigureAwait(false));
        }
        var templateBytes = JsonSerializer.SerializeToUtf8Bytes(template, Arch7bJson.CanonicalOptions);
        var value = new Arch7bOperationalExecutionAuthorityManifest(
            Arch7bV2Contracts.OperationalExecutionAuthorityManifestVersion,
            Convert.ToHexStringLower(SHA256.HashData(templateBytes)), inventory.EvidenceSha256,
            entries.Count, entries, string.Empty);
        var result = value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
        Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(inventory, result);
        return result;
    }

    private static async Task<Arch7bOperationalExecutionAuthority> MaterializeEntryAsync(
        string id, Arch7bOperationalAuthorityKind kind, string path, string outputRoot,
        Arch7bOneShotLivePlanTemplate template, IReadOnlyDictionary<string, string> authorityPaths,
        Arch7bRequiredOperationalExecutionAuthorityInventory inventory,
        CancellationToken cancellationToken)
    {
        string? fileSha = null;
        string? directorySha = null;
        string? inventoryPath = null;
        string? inventoryFileSha = null;
        string? repository = null;
        string? commit = null;
        string? tree = null;
        string? packageJsonSha = null;
        string? packageLockSha = null;
        string? runtimeClosureSha = null;
        string? runtimeVersion = null;
        if (kind is Arch7bOperationalAuthorityKind.File or Arch7bOperationalAuthorityKind.RootCa or
            Arch7bOperationalAuthorityKind.StaticConfig)
        {
            if (!File.Exists(path)) throw Missing(id);
            fileSha = Arch7bOperationalExecutionAuthorityValidator.FileSha(path);
        }
        else
        {
            if (!Directory.Exists(path)) throw Missing(id);
            var directoryInventory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(id, path);
            inventoryPath = System.IO.Path.Combine(outputRoot, id + "-directory-inventory.json");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(directoryInventory, Arch7bJson.CanonicalOptions);
            await File.WriteAllBytesAsync(inventoryPath, bytes, cancellationToken).ConfigureAwait(false);
            inventoryFileSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            directorySha = directoryInventory.EvidenceSha256;
        }
        if (kind == Arch7bOperationalAuthorityKind.GitRepository)
        {
            if (!authorityPaths.TryGetValue("git_executable", out var gitExecutable) ||
                !File.Exists(gitExecutable))
                throw Missing("git_executable");
            var reader = new Arch7bGitCoreRepositoryReader(path,
                System.IO.Path.GetFullPath(gitExecutable));
            commit = await reader.HeadAsync(cancellationToken).ConfigureAwait(false);
            tree = await reader.TreeAsync(cancellationToken).ConfigureAwait(false);
            repository = "https://github.com/phu-qqb/QQ.Production.Core.git";
            if (id == "core_repository" &&
                (commit != template.CoreCommit || tree != template.CoreTree)) throw Mismatch(id);
        }
        if (kind == Arch7bOperationalAuthorityKind.NodePackageRuntime)
        {
            var packageJson = System.IO.Path.Combine(path, "package.json");
            var packageLock = System.IO.Path.Combine(path, "package-lock.json");
            if (!File.Exists(packageJson) || !File.Exists(packageLock) ||
                !Directory.Exists(System.IO.Path.Combine(path, "node_modules"))) throw Mismatch(id);
            packageJsonSha = Arch7bOperationalExecutionAuthorityValidator.FileSha(packageJson);
            packageLockSha = Arch7bOperationalExecutionAuthorityValidator.FileSha(packageLock);
            runtimeClosureSha = Arch7bOperationalExecutionAuthorityValidator
                .DirectoryInventory(id + "-node-runtime", path).EvidenceSha256;
            if (!authorityPaths.TryGetValue("node_executable", out var nodePath))
                throw Missing("node_executable");
            runtimeVersion = RunVersion(nodePath);
        }
        if (kind == Arch7bOperationalAuthorityKind.DotnetRuntime)
        {
            var dotnet = System.IO.Path.Combine(path,
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (!File.Exists(dotnet)) throw Mismatch(id);
            runtimeVersion = RunVersion(dotnet);
            runtimeClosureSha = directorySha;
        }
        var references = inventory.References.Where(value => value.AuthorityId == id).ToArray();
        var value = new Arch7bOperationalExecutionAuthority(
            Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion, id, kind, path, fileSha,
            directorySha, inventoryPath, inventoryFileSha, repository, commit, tree,
            packageJsonSha, packageLockSha, runtimeClosureSha, runtimeVersion,
            references.Any(value => value.MustExist),
            references.Any(value => value.MustBeInsideRunRoot),
            "operational-authority-path-map", string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static IReadOnlyDictionary<string, string> ParsePathMapStrict(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw Mismatch("authority-path-map");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!result.TryAdd(property.Name, property.Value.GetString() ?? string.Empty))
                throw new Arch7bQualificationException(
                    Arch7bV2Contracts.OperationalAuthorityDuplicateId, property.Name);
        }
        return result;
    }

    private static string RunVersion(string executable)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start) ?? throw Mismatch("runtime-version");
        var output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(30_000) || process.ExitCode != 0) throw Mismatch("runtime-version");
        return output.Trim();
    }

    private static Arch7bQualificationException Missing(string id) =>
        new(Arch7bV2Contracts.OperationalAuthorityMissing, id);

    private static Arch7bQualificationException Mismatch(string id) =>
        new(Arch7bV2Contracts.OperationalAuthorityMismatch, id);
}
