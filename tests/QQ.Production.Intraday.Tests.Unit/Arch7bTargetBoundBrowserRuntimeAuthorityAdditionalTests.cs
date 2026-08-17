using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bTargetBoundBrowserRuntimeAuthorityAdditionalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-target-browser-extra", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Bracket_validation_uses_the_target_sha()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var chrome = authorities["chrome_executable"];
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);

        runner.ValidateBrowserAuthorityForQualification(
            Materialized("BRACKET_T2", ["--executable-path", chrome.Path]),
            chrome.Path, _ => false);
    }

    [Fact]
    public void Core_config_accepts_target_and_rejects_historical_compiled_sha()
    {
        Directory.CreateDirectory(root);
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var chrome = authorities["chrome_executable"];
        Assert.NotEqual(Arch7bSealedNonSecretEnvironment.QualifiedChromeExecutableSha256,
            chrome.Sha256);
        var runner = new Arch7bOneShotProcessRunnerV2(
            new Arch7bRealCommandAdapterRegistry(), authorities);
        var target = Config(chrome.Path, chrome.Sha256, "target.json");

        runner.ValidateBrowserAuthorityForQualification(
            Materialized("CORE_PREQUALIFICATION", ["--config", target]),
            chrome.Path, _ => false);

        var historical = Config(chrome.Path,
            Arch7bSealedNonSecretEnvironment.QualifiedChromeExecutableSha256,
            "historical.json");
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            runner.ValidateBrowserAuthorityForQualification(
                Materialized("CORE_PREQUALIFICATION", ["--config", historical]),
                chrome.Path, _ => false));
        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutableShaMismatch,
            error.BlockerCode);
    }

    [Fact]
    public void Wrong_target_sha_is_rejected_against_actual_file()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        authorities["chrome_executable"] = authorities["chrome_executable"] with
        { Sha256 = new string('0', 64) };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundBrowserRuntimeAuthorityGate.Qualify(
                Template(authorities), authorities));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutableShaMismatch,
            error.BlockerCode);
    }

    [Fact]
    public void Wrong_target_path_is_rejected_against_path_policy()
    {
        var authorities = Arch7bTaskkillTestAuthorities.Create();
        var node = authorities["node_executable"];
        authorities["chrome_executable"] = node with
        { AuthorityId = "chrome_executable" };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundBrowserRuntimeAuthorityGate.Qualify(
                Template(authorities), authorities));

        Assert.Equal(Arch7bV2Blockers.CommandChromeExecutablePathAuthorityMismatch,
            error.BlockerCode);
    }

    private string Config(string path, string sha, string name)
    {
        var target = Path.Combine(root, name);
        File.WriteAllBytes(target, JsonSerializer.SerializeToUtf8Bytes(new
        {
            browserExecutablePath = path,
            expectedBrowserExecutableSha256 = sha
        }));
        return target;
    }

    private static Arch7bOneShotMaterializedCommand Materialized(
        string stageId, IReadOnlyList<string> arguments)
    {
        var executable = typeof(Arch7bTargetBoundBrowserRuntimeAuthorityAdditionalTests)
            .Assembly.Location;
        var sha = Arch7bTaskkillTestAuthorities.Sha(executable);
        return new(Arch7bV2Contracts.MaterializedCommandVersion, "browser-command",
            stageId, Arch7bExecutionKind.ChildInvoke, executable, sha, arguments,
            Path.GetDirectoryName(executable)!, "qualification-adapter",
            Arch7bV2Contracts.ChildResultAdapterVersion, "qualification-output-v1",
            30, 4096, 4096, "qualification-process", false, false, false,
            [], [], null, executable, sha, Hash("materialized-command"));
    }

    private static Arch7bOneShotLivePlanTemplate Template(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var commands = new[]
        {
            Command("portal-proof", "PORTAL_SESSION_PROVEN"),
            Command("bracket", "BRACKET_T2")
        };
        var value = new Arch7bOneShotLivePlanTemplate(
            Arch7bV2Contracts.LivePlanTemplateVersion, Commit('a'), Commit('b'),
            Commit('c'), Commit('d'), Commit('e'), Commit('f'), Hash("manifest"),
            Hash("packet"), Hash("inventory"), Hash("repository"), Hash("tracked"),
            Hash("static"), Hash("commands"), Hash("adapters"), Hash("ca"),
            Hash("privilege"), Hash("calendar"), Hash("slo"), Hash("chronology"),
            Hash("cleanup"), "TEST", "1754288005", true, 1, 2, 1, 0,
            authorities, commands, [], string.Empty);
        return value with
        { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
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

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
