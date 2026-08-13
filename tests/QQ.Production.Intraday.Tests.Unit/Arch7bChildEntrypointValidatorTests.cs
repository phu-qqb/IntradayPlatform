using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bChildEntrypointValidatorTests : IDisposable
{
    private readonly List<string> roots = [];

    [Fact]
    public void Core_prequalification_module_is_resolved_from_core_node_runtime()
    {
        var fixture = RuntimeAuthority();

        var result = Arch7bChildEntrypointValidator.ValidateEntrypoint(
            Command(Arch7bChildEntrypointValidator.CorePrequalificationRelativeModulePath),
            fixture.Manifest);

        Assert.True(result.Passed);
        Assert.Equal("src/fast-seal-cli.mjs", result.InventoryRelativePath);
        Assert.Equal(fixture.ModulePath, result.ResolvedPath);
        Assert.Equal(Sha(fixture.ModulePath), result.FileSha256);
    }

    [Fact]
    public void Static_preflight_audits_all_13_commands_before_slot_selection()
    {
        var fixture = RuntimeAuthority();
        var evidencePath = Path.Combine(Root("valid-preflight"),
            Arch7bChildEntrypointValidator.ValidationFileName);

        var result = Arch7bChildEntrypointValidator.Validate(
            Template(Arch7bChildEntrypointValidator.CorePrequalificationRelativeModulePath),
            fixture.Manifest, evidencePath);

        Assert.Equal(13, result.CommandCount);
        Assert.Equal(13, result.Commands.Count);
        Assert.Equal(1, result.RelativeEntrypointCount);
        Assert.Equal(1, result.ValidatedEntrypointCount);
        Assert.Equal(0, result.InvalidEntrypointCount);
        Assert.True(File.Exists(evidencePath));
    }

    [Fact]
    public void Static_preflight_rejects_invalid_path_before_calendar_or_slot_markers()
    {
        var fixture = RuntimeAuthority();
        var preflightRoot = Root("invalid-preflight");
        var evidencePath = Path.Combine(preflightRoot,
            Arch7bChildEntrypointValidator.ValidationFileName);

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bChildEntrypointValidator.Validate(
                Template("tools/lmax_portal_reports_downloader/src/fast-seal-cli.mjs"),
                fixture.Manifest, evidencePath));

        Assert.Equal(Arch7bV2Blockers.ChildEntrypointPathInvalid, error.BlockerCode);
        Assert.False(File.Exists(evidencePath));
        Assert.False(File.Exists(Path.Combine(preflightRoot, "calendar-loaded.json")));
        Assert.False(File.Exists(Path.Combine(preflightRoot, "slot-selected.json")));
        Assert.False(File.Exists(Path.Combine(preflightRoot, "slot-locked.json")));
    }

    [Theory]
    [InlineData("tools/lmax_portal_reports_downloader/src/fast-seal-cli.mjs")]
    [InlineData("src/missing.mjs")]
    [InlineData("C:/absolute/fast-seal-cli.mjs")]
    public void Invalid_core_prequalification_module_path_is_rejected(string argument)
    {
        var fixture = RuntimeAuthority();

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bChildEntrypointValidator.ValidateEntrypoint(
                Command(argument), fixture.Manifest));

        Assert.Equal(Arch7bV2Blockers.ChildEntrypointPathInvalid, error.BlockerCode);
    }

    [Fact]
    public void Relative_entrypoint_outside_working_directory_is_rejected()
    {
        var fixture = RuntimeAuthority();

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bChildEntrypointValidator.ValidateEntrypoint(
                Command("../outside.mjs", "MARKET_PREARM"), fixture.Manifest));

        Assert.Equal(Arch7bV2Blockers.ChildEntrypointOutsideWorkingDirectory,
            error.BlockerCode);
    }

    [Fact]
    public void Reparse_point_in_entrypoint_chain_is_rejected()
    {
        var fixture = RuntimeAuthority();

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bChildEntrypointValidator.ValidateEntrypoint(
                Command(Arch7bChildEntrypointValidator.CorePrequalificationRelativeModulePath),
                fixture.Manifest, path => SamePath(path, fixture.ModulePath)));

        Assert.Equal(Arch7bV2Blockers.ChildEntrypointPathInvalid, error.BlockerCode);
    }

    [Fact]
    public void Entrypoint_sha_drift_from_closed_runtime_inventory_is_rejected()
    {
        var fixture = RuntimeAuthority();
        File.AppendAllText(fixture.ModulePath, "// drift\n");

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bChildEntrypointValidator.ValidateEntrypoint(
                Command(Arch7bChildEntrypointValidator.CorePrequalificationRelativeModulePath),
                fixture.Manifest));

        Assert.Equal(Arch7bV2Blockers.ChildEntrypointShaMismatch, error.BlockerCode);
    }

    private RuntimeFixture RuntimeAuthority()
    {
        var root = Root("core-node-runtime");
        var sourceRoot = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceRoot);
        var modulePath = Path.Combine(sourceRoot, "fast-seal-cli.mjs");
        File.WriteAllText(modulePath, "export const qualified = true;\n");
        var inventory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(
            "core_node_runtime", root);
        var manifestRoot = Root("inventory");
        Directory.CreateDirectory(manifestRoot);
        var inventoryPath = Path.Combine(manifestRoot, "core-node-runtime.json");
        var inventoryBytes = JsonSerializer.SerializeToUtf8Bytes(inventory,
            Arch7bJson.CanonicalOptions);
        File.WriteAllBytes(inventoryPath, inventoryBytes);
        var authority = Seal(new Arch7bOperationalExecutionAuthority(
            Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion,
            "core_node_runtime", Arch7bOperationalAuthorityKind.DirectoryInventory,
            root, null, inventory.EvidenceSha256, inventoryPath,
            Convert.ToHexStringLower(SHA256.HashData(inventoryBytes)), null, null, null,
            null, null, inventory.EvidenceSha256, null, true, false, "test", string.Empty));
        var provisional = new Arch7bOperationalExecutionAuthorityManifest(
            Arch7bV2Contracts.OperationalExecutionAuthorityManifestVersion,
            new('a', 64), new('b', 64), 1, [authority], string.Empty);
        var manifest = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
        return new(root, modulePath, manifest);
    }

    private Arch7bOneShotCommandTemplate Command(string argument,
        string stage = "CORE_PREQUALIFICATION")
    {
        var fixture = Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, Root("command"));
        return fixture.Template.CommandTemplates[0] with
        {
            CommandId = stage.ToLowerInvariant(),
            StageId = stage,
            ExecutableAuthorityId = "node_executable",
            WorkingDirectoryAuthorityId = "core_node_runtime",
            ArgumentTemplates =
            [
                new(argument, Arch7bPlaceholderValueKind.Literal, null, -1, false)
            ]
        };
    }

    private Arch7bOneShotLivePlanTemplate Template(string coreArgument)
    {
        var fixture = Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, Root("template"));
        var prototype = fixture.Template.CommandTemplates[0];
        var commands = Enumerable.Range(0, 13).Select(index => prototype with
        {
            CommandId = "entrypoint-audit-" + index,
            StageId = index == 0 ? "CORE_PREQUALIFICATION" : "STATIC_" + index,
            ExecutableAuthorityId = index == 0 ? "node_executable" : "dotnet_executable",
            WorkingDirectoryAuthorityId = "core_node_runtime",
            ArgumentTemplates =
            [
                new(index == 0 ? coreArgument : "non-node-argument",
                    Arch7bPlaceholderValueKind.Literal, null, -1, false)
            ],
            EvidenceSha256 = Arch7bOneShotContracts.Sha256("entrypoint-audit-" + index)
        }).ToArray();
        var provisional = fixture.Template with
        {
            CommandTemplates = commands,
            EvidenceSha256 = string.Empty
        };
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
    }

    private static Arch7bOperationalExecutionAuthority Seal(
        Arch7bOperationalExecutionAuthority value) => value with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical())
        };

    private string Root(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "qq-arch7b-child-entrypoint",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        roots.Add(path);
        return path;
    }

    private static string Sha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool SamePath(string left, string right) => string.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        foreach (var root in roots.OrderByDescending(value => value.Length))
            if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed record RuntimeFixture(string Root, string ModulePath,
        Arch7bOperationalExecutionAuthorityManifest Manifest);
}
