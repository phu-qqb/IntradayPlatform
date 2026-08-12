using System.Diagnostics;
using System.Reflection;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCorePrequalificationMsEdgeEnvironmentTests
{
    private const string CommandId = "core-runtime-prequalification";
    private const string StageId = "CORE_PREQUALIFICATION";

    [Fact]
    public void Exact_msedge_authority_and_program_files_x86_binding_are_accepted_deterministically()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();

        var first = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);
        var second = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);

        Assert.Equal(first, second);
        Assert.Equal(2, first.Count);
        Assert.Equal(new[] { "PATH", "ProgramFiles(x86)" },
            first.Select(value => value.VariableName));
        var programFiles = first.Single(value => value.VariableName == "ProgramFiles(x86)");
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            programFiles.Value);
        Assert.Equal(Arch7bSealedNonSecretEnvironment
            .CorePrequalificationProgramFilesX86AuthorityId,
            programFiles.SourceAuthorityId);
        Assert.Equal(first, Arch7bSealedNonSecretEnvironment.ValidateTemplate(
            first, authorities, CommandId, StageId));
        Arch7bSealedNonSecretEnvironment.ValidateMaterialized(first,
            authorities["node_executable"].Path, CommandId, StageId);
        var stale = first.Select(value => value.VariableName == "ProgramFiles(x86)"
            ? value with { SourceAuthoritySha256 = new string('0', 64) }
            : value).ToArray();
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutableShaMismatch,
            () => Arch7bSealedNonSecretEnvironment.ValidateMaterialized(stale,
                authorities["node_executable"].Path, CommandId, StageId));
    }

    [Fact]
    public void Missing_msedge_authority_is_rejected()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        authorities.Remove("msedge_executable");

        AssertBlocker(Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing,
            () => Arch7bSealedNonSecretEnvironment
                .ForCorePrequalificationEnvironment(authorities));
    }

    [Fact]
    public void Wrong_msedge_path_filename_parent_and_sha_are_rejected()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var correct = authorities["msedge_executable"];
        var node = authorities["node_executable"];
        var badPath = new Dictionary<string, Arch7bFileAuthority>(authorities,
            StringComparer.Ordinal) { ["msedge_executable"] = node with
            { AuthorityId = "msedge_executable" } };
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment
                .ForCorePrequalificationEnvironment(badPath));

        var wrongFilename = new Dictionary<string, Arch7bFileAuthority>(authorities,
            StringComparer.Ordinal) { ["msedge_executable"] = correct with
            { Path = Path.Combine(Path.GetDirectoryName(correct.Path)!, "edge.exe") } };
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment
                .ForCorePrequalificationEnvironment(wrongFilename));

        var wrongParent = new Dictionary<string, Arch7bFileAuthority>(authorities,
            StringComparer.Ordinal) { ["msedge_executable"] = correct with
            { Path = Path.Combine(Path.GetTempPath(), "msedge.exe") } };
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment
                .ForCorePrequalificationEnvironment(wrongParent));

        var badSha = new Dictionary<string, Arch7bFileAuthority>(authorities,
            StringComparer.Ordinal) { ["msedge_executable"] = correct with
            { Sha256 = new string('0', 64) } };
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutableShaMismatch,
            () => Arch7bSealedNonSecretEnvironment
                .ForCorePrequalificationEnvironment(badSha));
    }

    [Fact]
    public void Reparse_point_on_edge_or_any_required_parent_is_rejected()
    {
        var authority = Arch7bTaskkillTestAuthorities.Create()["msedge_executable"];
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var chain = new[]
        {
            root,
            Path.Combine(root, "Microsoft"),
            Path.Combine(root, "Microsoft", "Edge"),
            Path.Combine(root, "Microsoft", "Edge", "Application"),
            authority.Path
        };

        foreach (var rejected in chain)
            AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
                () => Arch7bSealedNonSecretEnvironment.ValidateMsEdgeAuthority(
                    authority, root, candidate => string.Equals(
                        Path.GetFullPath(candidate), Path.GetFullPath(rejected),
                        StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Core_scope_requires_exact_sealed_pair_and_other_commands_reject_program_files_x86()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var environment = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);
        var path = environment.Single(value => value.VariableName == "PATH");
        var programFiles = environment.Single(value => value.VariableName == "ProgramFiles(x86)");

        AssertBlocker(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                [path], authorities, CommandId, StageId));
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                [path, programFiles with { Value = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles) }], authorities, CommandId, StageId));
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                [path, programFiles with { Value = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData) }], authorities, CommandId, StageId));
        AssertBlocker(Arch7bV2Blockers.CommandMsEdgeExecutablePathAuthorityMismatch,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                [path, programFiles with { SourceAuthorityId = "ambient_parent_environment" }],
                authorities, CommandId, StageId));
        AssertBlocker(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                environment, authorities, "market-capture", "MARKET_CAPTURE"));
        AssertBlocker(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
            () => Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                [], authorities, CommandId, StageId));
    }

    [Fact]
    public void Process_start_info_contains_sealed_program_files_only_for_core_prequalification()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var environment = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities);
        var node = authorities["node_executable"];
        var command = new Arch7bOneShotMaterializedCommand(
            Arch7bV2Contracts.MaterializedCommandVersion, CommandId, StageId,
            Arch7bExecutionKind.ChildInvoke, node.Path, node.Sha256, [],
            Path.GetDirectoryName(node.Path)!, "adapter",
            Arch7bV2Contracts.ChildResultAdapterVersion, "native", 30,
            1_048_576, 1_048_576, "qualification-child-process", false,
            false, false, [], environment, null, Path.Combine(Path.GetTempPath(),
                "command-authority.json"), new string('a', 64), new string('b', 64));
        var method = typeof(Arch7bOneShotProcessRunnerV2).GetMethod("BuildStartInfo",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException();
        var lease = new Arch7bSecretEnvironmentLease(
            Arch7bV2Contracts.SecretEnvironmentInjectionVersion, CommandId,
            new Dictionary<string, string>(), 0, false);

        var start = (ProcessStartInfo)(method.Invoke(null, [command, lease]) ??
            throw new InvalidOperationException());

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            start.Environment["ProgramFiles(x86)"]);
        Assert.Equal(environment.Single(value => value.VariableName == "PATH").Value,
            start.Environment["PATH"]);
        Assert.False(start.Environment.ContainsKey("PROGRAMFILES"));
        Assert.False(start.Environment.ContainsKey("HOMEDRIVE"));
        var inherited = (string[])(typeof(Arch7bOneShotProcessRunnerV2)
            .GetField("InheritedSystemVariables", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) ?? throw new InvalidOperationException());
        Assert.DoesNotContain("ProgramFiles(x86)", inherited, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("PROGRAMFILES", inherited, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("HOMEDRIVE", inherited, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertBlocker(string expected, Action action) =>
        Assert.Equal(expected,
            Assert.Throws<Arch7bQualificationException>(action).BlockerCode);
}
