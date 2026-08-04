using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bSealedNonSecretEnvironmentTests
{
    [Fact]
    public void Dotnet_root_is_the_only_allowed_non_secret_variable_and_is_authority_bound()
    {
        var authorities = DotnetAuthorities();
        var environment = Arch7bSealedNonSecretEnvironment.ForDotnetRoot(authorities);

        Assert.Single(environment);
        Assert.Equal("DOTNET_ROOT", environment[0].VariableName);
        Assert.Equal(Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            environment[0].ContractVersion);
        Assert.Empty(Arch7bSealedNonSecretEnvironment.ValidateTemplate([], authorities));
        Assert.Equal(Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ForDotnetRoot(
                    new Dictionary<string, Arch7bFileAuthority>())).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                    [environment[0] with { VariableName = "PATH" }], authorities)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                    [environment[0] with { VariableName = "AWS_SECRET_ACCESS_KEY" }], authorities)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                    [environment[0] with { Value = Path.GetTempPath() }], authorities)).BlockerCode);
    }

    [Fact]
    public void Dotnet_root_rejects_wrong_executable_sha_and_executable_outside_the_root()
    {
        var authorities = DotnetAuthorities();
        var badSha = new Dictionary<string, Arch7bFileAuthority>(authorities, StringComparer.Ordinal)
        {
            ["dotnet_executable"] = authorities["dotnet_executable"] with { Sha256 = new string('0', 64) }
        };
        Assert.Equal(Arch7bV2Blockers.CommandDotnetExecutableShaMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ForDotnetRoot(badSha)).BlockerCode);

        var outside = new Dictionary<string, Arch7bFileAuthority>(authorities, StringComparer.Ordinal)
        {
            ["dotnet_executable"] = authorities["dotnet_executable"] with
            {
                Path = SupervisorExecutable(),
                Sha256 = Sha(SupervisorExecutable())
            }
        };
        Assert.Equal(Arch7bV2Blockers.CommandDotnetRootAuthorityMismatch,
            Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bSealedNonSecretEnvironment.ForDotnetRoot(outside)).BlockerCode);
    }

    [Fact]
    public async Task Materialized_command_carries_the_sealed_map_and_its_hash_changes_with_dotnet_root()
    {
        var root = Root("materialized");
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), root,
            dotnetRoot: DotnetRoot());
        Directory.CreateDirectory(root);
        var store = new Arch7bOneShotLiveFactStore(root);
        var now = DateTimeOffset.UtcNow;
        store.Append("runtime_run_root", "STATIC_AUTHORITY_VALIDATION", new { path = root },
            Arch7bOneShotContracts.Sha256(root), now);
        var command = fixture.Template.CommandTemplates.First();
        var materialized = await new Arch7bOneShotCommandMaterializer().MaterializeAsync(command, store,
            fixture.Template.FileAuthorities, root, now);

        Assert.Single(materialized.NonSecretEnvironment);
        Assert.Equal("DOTNET_ROOT", materialized.NonSecretEnvironment[0].VariableName);
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(materialized.AuthorityPath));
        Assert.True(document.RootElement.TryGetProperty("non_secret_environment", out var serialized));
        Assert.Equal(JsonValueKind.Array, serialized.ValueKind);

        var alternateRoot = Path.Combine(root, "alternate-dotnet");
        Directory.CreateDirectory(alternateRoot);
        File.Copy(Path.Combine(DotnetRoot(), "dotnet.exe"), Path.Combine(alternateRoot, "dotnet.exe"));
        var changed = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), Root("changed"),
            dotnetRoot: alternateRoot);
        Assert.NotEqual(command.EvidenceSha256, changed.Template.CommandTemplates.First().EvidenceSha256);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Framework_dependent_children_use_sealed_dotnet_root_and_complete_without_premature_eof()
    {
        var root = Root("runtime");
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), root,
            dotnetRoot: DotnetRoot());
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);

        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, root,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease());

        Assert.True(result.Passed, JsonSerializer.Serialize(result));
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.ResidualMarkerCount);
        Assert.All(fixture.Template.CommandTemplates, command =>
            Assert.Contains(command.NonSecretEnvironment, value => value.VariableName == "DOTNET_ROOT"));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Runner_builds_from_the_sealed_map_without_inheriting_dotnet_root()
    {
        var root = Root("start-info");
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), root,
            dotnetRoot: DotnetRoot());
        Directory.CreateDirectory(root);
        var store = new Arch7bOneShotLiveFactStore(root);
        var now = DateTimeOffset.UtcNow;
        store.Append("runtime_run_root", "STATIC_AUTHORITY_VALIDATION", new { path = root },
            Arch7bOneShotContracts.Sha256(root), now);
        var materialized = await new Arch7bOneShotCommandMaterializer().MaterializeAsync(
            fixture.Template.CommandTemplates.First(), store, fixture.Template.FileAuthorities, root, now);
        var method = typeof(Arch7bOneShotProcessRunnerV2).GetMethod("BuildStartInfo",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException();
        var lease = new Arch7bSecretEnvironmentLease(Arch7bV2Contracts.SecretEnvironmentInjectionVersion,
            materialized.CommandId, new Dictionary<string, string>(), 0, false);
        var startInfo = (ProcessStartInfo)(method.Invoke(null, [materialized, lease]) ??
            throw new InvalidOperationException());

        Assert.Equal(DotnetRoot(), startInfo.Environment["DOTNET_ROOT"]);
        Assert.DoesNotContain("DOTNET_ROOT", startInfo.Environment.Keys.Where(key =>
            key != "DOTNET_ROOT"));
        Directory.Delete(root, true);
    }

    private static Dictionary<string, Arch7bFileAuthority> DotnetAuthorities()
    {
        var root = DotnetRoot();
        var executable = Path.Combine(root, "dotnet.exe");
        return new(StringComparer.Ordinal)
        {
            ["dotnet_root"] = new("dotnet_root", root,
                Arch7bOneShotContracts.Sha256("arch7b_dotnet_root_authority_v1\n" + root), true, false),
            ["dotnet_executable"] = new("dotnet_executable", executable, Sha(executable), true, false)
        };
    }

    private static string DotnetRoot()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "dotnet.exe")))
            return Path.GetFullPath(configured);
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (!File.Exists(Path.Combine(installed, "dotnet.exe"))) throw new DirectoryNotFoundException(installed);
        return installed;
    }

    private static string Root(string suffix) => Path.Combine(Path.GetTempPath(),
        "qq-arch7b-non-secret-environment-tests", suffix + "-" + Guid.NewGuid().ToString("N"));

    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string SupervisorExecutable()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        var repository = current?.FullName ?? throw new DirectoryNotFoundException("repository root");
        return Path.Combine(repository, "tools", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "bin", "Release", "net10.0", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.exe");
    }
}