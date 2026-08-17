using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bTargetBoundBrowserRuntimeAuthorityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-target-browser", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Gate_qualifies_the_target_bound_chrome_authority()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var template = Template(authorities);

        var result = Arch7bTargetBoundBrowserRuntimeAuthorityGate.Qualify(
            template, authorities);

        var chrome = authorities["chrome_executable"];
        Assert.Equal(Arch7bTargetBoundBrowserRuntimeAuthorityGate.Verdict,
            result.Verdict);
        Assert.Equal(chrome.Path, result.Path);
        Assert.Equal(chrome.Sha256, result.Sha256);
        Assert.Equal(1, result.PortalPathBindingCount);
        Assert.Equal(1, result.BracketPathBindingCount);
        Assert.True(result.CorePrequalificationUsesTargetAuthority);
        Assert.True(result.BrowserChannelAbsent);
        Assert.False(result.CompiledContentShaUsed);
        Assert.True(Arch7bOneShotContracts.IsSha256(result.EvidenceSha256));
    }

    [Fact]
    public void Gate_rejects_a_target_authority_different_from_the_template()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var target = new Dictionary<string, Arch7bFileAuthority>(authorities,
            StringComparer.Ordinal)
        {
            ["chrome_executable"] = authorities["chrome_executable"] with
            { Sha256 = new string('0', 64) }
        };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundBrowserRuntimeAuthorityGate.Qualify(
                Template(authorities), target));

        Assert.Equal(Arch7bV2Blockers.AuthorityBindingMismatch, error.BlockerCode);
    }

    [Fact]
    public void Gate_rejects_browser_channel_configuration()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var template = Template(authorities);
        var commands = template.CommandTemplates.Select(command =>
            command.StageId == "PORTAL_SESSION_PROVEN"
                ? command with
                {
                    ArgumentTemplates = command.ArgumentTemplates.Append(new(
                        "--browserChannel", Arch7bPlaceholderValueKind.Literal,
                        null, -1, false)).ToArray()
                }
                : command).ToArray();

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundBrowserRuntimeAuthorityGate.Qualify(
                template with { CommandTemplates = commands }, authorities));

        Assert.Equal(Arch7bV2Blockers.CommandTemplateInvalid, error.BlockerCode);
    }

    [Fact]
    public void Portal_validation_uses_the_target_sha_not_the_compiled_sha()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var chrome = authorities["chrome_executable"];
        Assert.NotEqual(Arch7bSealedNonSecretEnvironment.QualifiedChromeExecutableSha256,
            chrome.Sha256);
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);

        runner.ValidateBrowserAuthorityForQualification(
            Materialized("PORTAL_SESSION_PROVEN",
                ["--executable-path", chrome.Path]), chrome.Path, _ => false);
    }

    [Fact]
    public void Core_prequalification_config_sha_must_match_the_target_authority()
    {
        Directory.CreateDirectory(root);
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var chrome = authorities["chrome_executable"];
        var configPath = Path.Combine(root, "core-prequalification-config.json");
        File.WriteAllBytes(configPath, JsonSerializer.SerializeToUtf8Bytes(new
        {
            browserExecutablePath = chrome.Path,
            expectedBrowserExecutableSha256 = new string('0', 64)
        }));
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            runner.ValidateBrowserAuthorityForQualification(
                Materialized("CORE_PREQUALIFICATION", ["--config", configPath]),
                chrome.Path, _ => false));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutableShaMismatch,
            error.BlockerCode);
    }

    [Fact]
    public void Chrome_changed_after_materialization_is_rejected()
    {
        var directory = Path.Combine(root, "mutable");
        Directory.CreateDirectory(directory);
        var source = Arch7bTaskkillTestAuthorities.Create()["chrome_executable"];
        var path = Path.Combine(directory, "chrome.exe");
        File.Copy(source.Path, path);
        var authority = Arch7bTaskkillTestAuthorities.FileAuthority(
            "chrome_executable", path);
        var authorities = new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        {
            ["chrome_executable"] = authority
        };
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);
        var command = Materialized("PORTAL_SESSION_PROVEN",
            ["--executable-path", path]);
        runner.ValidateBrowserAuthorityForQualification(command, path, _ => false);

        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write,
                   FileShare.Read))
            stream.WriteByte(0);

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            runner.ValidateBrowserAuthorityForQualification(command, path, _ => false));
        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutableShaMismatch,
            error.BlockerCode);
    }

    [Fact]
    public void Reparse_point_in_target_path_is_rejected_at_spawn_validation()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var chrome = authorities["chrome_executable"];
        var parent = Path.GetDirectoryName(chrome.Path)!;
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            runner.ValidateBrowserAuthorityForQualification(
                Materialized("BRACKET_T2", ["--executable-path", chrome.Path]),
                chrome.Path, path => string.Equals(path, parent,
                    StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutablePathAuthorityMismatch,
            error.BlockerCode);
    }

    [Fact]
    public void Live_process_runner_has_no_compiled_chrome_content_identity()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "Arch7bBoundedProcessRuntime.cs"));

        Assert.DoesNotContain("QualifiedChromeExecutableSha256", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("QualifiedChromeVersion", source,
            StringComparison.Ordinal);
    }

    private static Arch7bOneShotMaterializedCommand Materialized(
        string stageId, IReadOnlyList<string> arguments)
    {
        var executable = typeof(Arch7bTargetBoundBrowserRuntimeAuthorityTests)
            .Assembly.Location;
        return new(Arch7bV2Contracts.MaterializedCommandVersion,
            "browser-command", stageId, Arch7bExecutionKind.ChildInvoke,
            executable, Arch7bTaskkillTestAuthorities.Sha(executable), arguments,
            Path.GetDirectoryName(executable)!, "qualification-adapter",
            Arch7bV2Contracts.ChildResultAdapterVersion, "qualification-output-v1",
            30, 4096, 4096, "qualification-process", false, false, false,
            [], [], null, executable, Arch7bTaskkillTestAuthorities.Sha(executable),
            Hash("materialized-command"));
    }

    private static Arch7bOneShotLivePlanTemplate Template(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var commands = new[]
        {
            Command("portal-proof", "PORTAL_SESSION_PROVEN"),
            Command("bracket", "BRACKET_T2")
        };
        var provisional = new Arch7bOneShotLivePlanTemplate(
            Arch7bV2Contracts.LivePlanTemplateVersion, Commit('a'), Commit('b'),
            Commit('c'), Commit('d'), Commit('e'), Commit('f'), Hash("manifest"),
            Hash("packet"), Hash("inventory"), Hash("repository"),
            Hash("tracked"), Hash("static"), Hash("commands"), Hash("adapters"),
            Hash("ca"), Hash("privilege"), Hash("calendar"), Hash("slo"),
            Hash("chronology"), Hash("cleanup"), "TEST", "1754288005", true,
            1, 2, 1, 0, authorities, commands, [], string.Empty);
        return provisional with
        { EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical()) };
    }

    private static Arch7bOneShotCommandTemplate Command(string id, string stageId) =>
        new(Arch7bV2Contracts.CommandTemplateVersion, id, stageId,
            Arch7bExecutionKind.ChildInvoke, "node_executable",
            [
                new("--executable-path", Arch7bPlaceholderValueKind.Literal,
                    null, -1, false),
                new("${authority:chrome_executable.path}",
                    Arch7bPlaceholderValueKind.AbsolutePath, null, -1, false)
            ], "core_node_runtime", "qualification-adapter",
            Arch7bV2Contracts.ChildResultAdapterVersion, "qualification-output-v1",
            30, 4096, 4096, "qualification-process", false, false, false,
            [], [], null, Hash(id));

    private static string Commit(char value) => new(value, 40);
    private static string Hash(string value) => Arch7bOneShotContracts.Sha256(value);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
