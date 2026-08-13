using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bChildEntrypointValidationItem(
    string CommandId,
    string StageId,
    string ExecutableAuthorityId,
    string WorkingDirectoryAuthorityId,
    bool HasRelativeEntrypoint,
    string? EntrypointArgument,
    string? ResolvedPath,
    string? InventoryRelativePath,
    string? FileSha256,
    bool Passed,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', CommandId, StageId, ExecutableAuthorityId,
        WorkingDirectoryAuthorityId, HasRelativeEntrypoint, EntrypointArgument ?? string.Empty,
        ResolvedPath ?? string.Empty, InventoryRelativePath ?? string.Empty,
        FileSha256 ?? string.Empty, Passed);
}

public sealed record Arch7bChildEntrypointValidation(
    string ContractVersion,
    int CommandCount,
    int RelativeEntrypointCount,
    int ValidatedEntrypointCount,
    int InvalidEntrypointCount,
    IReadOnlyList<Arch7bChildEntrypointValidationItem> Commands,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, CommandCount,
        RelativeEntrypointCount, ValidatedEntrypointCount, InvalidEntrypointCount,
        string.Join('|', Commands.Select(value => value.EvidenceSha256)));
}

public static class Arch7bChildEntrypointValidator
{
    public const string Version = "arch7b_child_entrypoint_validation_v1";
    public const string ValidationFileName = "arch7b-child-entrypoint-validation-v1.json";
    public const string CorePrequalificationRelativeModulePath = "src/fast-seal-cli.mjs";

    public static Arch7bChildEntrypointValidation Validate(
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOperationalExecutionAuthorityManifest manifest,
        string? evidencePath = null)
    {
        template.ValidateEvidence();
        manifest.ValidateEvidence();
        if (template.CommandTemplates.Count != Arch7bFinalStageExecutionCatalog.CommandTemplateCount)
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, "command-count");

        var commands = template.CommandTemplates.Select(command =>
            IsRelativeEntrypointCommand(command)
                ? ValidateEntrypoint(command, manifest)
                : Seal(new(command.CommandId, command.StageId, command.ExecutableAuthorityId,
                    command.WorkingDirectoryAuthorityId, false, null, null, null, null, true,
                    string.Empty))).ToArray();
        var relativeCount = commands.Count(value => value.HasRelativeEntrypoint);
        var value = new Arch7bChildEntrypointValidation(Version, commands.Length, relativeCount,
            commands.Count(item => item.HasRelativeEntrypoint && item.Passed),
            commands.Count(item => !item.Passed), commands, string.Empty);
        var result = value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };

        if (evidencePath is not null)
        {
            evidencePath = Path.GetFullPath(evidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            using var stream = new FileStream(evidencePath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough);
            JsonSerializer.Serialize(stream, result, Arch7bJson.CanonicalOptions);
            stream.Flush(true);
        }
        return result;
    }

    internal static Arch7bChildEntrypointValidationItem ValidateEntrypoint(
        Arch7bOneShotCommandTemplate command,
        Arch7bOperationalExecutionAuthorityManifest manifest,
        Func<string, bool>? isReparsePoint = null)
    {
        isReparsePoint ??= IsReparsePoint;
        if (!IsRelativeEntrypointCommand(command) || command.ArgumentTemplates.Count == 0 ||
            command.ArgumentTemplates[0].ValueKind != Arch7bPlaceholderValueKind.Literal)
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);

        var argument = command.ArgumentTemplates[0].Value;
        if (!IsResolvableRelativePath(argument))
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);

        var authority = manifest.Authorities.SingleOrDefault(value =>
            value.AuthorityId == command.WorkingDirectoryAuthorityId) ??
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid,
                command.WorkingDirectoryAuthorityId);
        var workingDirectory = Path.GetFullPath(authority.Path);
        if (!Directory.Exists(workingDirectory))
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);
        var resolved = Path.GetFullPath(Path.Combine(workingDirectory,
            argument.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsUnder(workingDirectory, resolved))
            throw Failure(Arch7bV2Blockers.ChildEntrypointOutsideWorkingDirectory,
                command.CommandId);
        if (!IsCanonicalRelativePath(argument) ||
            command.StageId == "CORE_PREQUALIFICATION" &&
            argument != CorePrequalificationRelativeModulePath)
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);
        var relative = Path.GetRelativePath(workingDirectory, resolved).Replace('\\', '/');
        if (relative != argument || !File.Exists(resolved) || Directory.Exists(resolved))
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);

        for (var current = resolved; current is not null && IsUnderOrEqual(workingDirectory, current);
             current = Path.GetDirectoryName(current))
            if (isReparsePoint(current))
                throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);

        var inventory = LoadInventory(authority, command.CommandId);
        var entry = inventory.Entries.SingleOrDefault(value =>
            value.RelativePath == relative && value.EntryType == "FILE");
        if (entry is null || entry.ReparsePoint ||
            !Arch7bOneShotContracts.IsSha256(entry.FileSha256 ?? string.Empty))
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, command.CommandId);
        var actualSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(resolved)));
        if (entry.FileSha256 != actualSha)
            throw Failure(Arch7bV2Blockers.ChildEntrypointShaMismatch, command.CommandId);

        return Seal(new(command.CommandId, command.StageId, command.ExecutableAuthorityId,
            command.WorkingDirectoryAuthorityId, true, argument, resolved, relative, actualSha,
            true, string.Empty));
    }

    private static Arch7bOperationalDirectoryInventory LoadInventory(
        Arch7bOperationalExecutionAuthority authority, string commandId)
    {
        if (authority.InventoryManifestPath is null || authority.InventoryManifestSha256 is null ||
            !File.Exists(authority.InventoryManifestPath))
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, commandId);
        var bytes = File.ReadAllBytes(authority.InventoryManifestPath);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != authority.InventoryManifestSha256)
            throw Failure(Arch7bV2Blockers.ChildEntrypointShaMismatch, commandId);
        var inventory = JsonSerializer.Deserialize<Arch7bOperationalDirectoryInventory>(bytes,
            Arch7bJson.CanonicalOptions) ??
            throw Failure(Arch7bV2Blockers.ChildEntrypointPathInvalid, commandId);
        inventory.ValidateEvidence();
        if (inventory.AuthorityId != authority.AuthorityId ||
            !SamePath(inventory.AbsolutePath, authority.Path) ||
            inventory.EvidenceSha256 != authority.DirectoryInventorySha256)
            throw Failure(Arch7bV2Blockers.ChildEntrypointShaMismatch, commandId);
        return inventory;
    }

    private static bool IsRelativeEntrypointCommand(Arch7bOneShotCommandTemplate command) =>
        command.ExecutableAuthorityId == "node_executable";

    private static bool IsResolvableRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() &&
        !Path.IsPathFullyQualified(value) && !value.Contains('\\') && !value.Contains(':');

    private static bool IsCanonicalRelativePath(string value)
    {
        if (!IsResolvableRelativePath(value)) return false;
        var segments = value.Split('/');
        return segments.Length > 1 && segments.All(segment =>
            segment.Length != 0 && segment is not "." and not "..");
    }

    private static bool IsUnder(string root, string path) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderOrEqual(string root, string path) =>
        SamePath(root, path) || IsUnder(root, path);

    private static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
        StringComparison.OrdinalIgnoreCase);

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static Arch7bChildEntrypointValidationItem Seal(
        Arch7bChildEntrypointValidationItem value) => value with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical())
        };

    private static Arch7bQualificationException Failure(string blocker, string detail) =>
        new(blocker, detail);
}
