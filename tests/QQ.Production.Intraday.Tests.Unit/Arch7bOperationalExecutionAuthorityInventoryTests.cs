using System.Security.Cryptography;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOperationalExecutionAuthorityInventoryTests
{
    [Fact]
    public void Operational_template_derives_the_closed_authority_inventory()
    {
        var repositoryRoot = RepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-authority-inventory",
            Guid.NewGuid().ToString("N"));
        var sourceManifest = Path.Combine(repositoryRoot, "docs", "architecture", "arch7b",
            "arch7b-position-market-live-command-manifest.json");
        var compiled = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            OperationalSkeleton(root), File.ReadAllBytes(sourceManifest));

        var inventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder.Build(
            compiled.Template);

        Assert.Equal(40, inventory.StageCount);
        Assert.Equal(13, inventory.CommandTemplateCount);
        Assert.Equal(6, Arch7bOperationalLiveFactBindingCatalog.Build().Count);
        Assert.Equal(34, compiled.BindingCount);
        Assert.Equal(0, inventory.DuplicateConflictingReferenceCount);
        Assert.Equal(0, inventory.UnknownAuthorityKindCount);
        Assert.Equal(0, inventory.UnresolvedReferenceCount);
        Assert.Equal(inventory.RequiredAuthorityIds.Count, inventory.RequiredAuthorityIdCount);
        foreach (var authorityId in new[]
                 {
                     "core_repository", "core_node_runtime", "intraday_runtime", "git_executable",
                     "node_executable", "taskkill_executable", "chrome_executable",
                     "dotnet_executable", "dotnet_root", "root_certificate", "market_data_config"
                 })
            Assert.Contains(authorityId, inventory.RequiredAuthorityIds);
        var taskkillReferences = inventory.References.Where(value =>
            value.AuthorityId == "taskkill_executable").ToArray();
        Assert.Single(taskkillReferences);
        Assert.Equal("CORE_PREQUALIFICATION", taskkillReferences[0].ReferencingStageId);
        Assert.Equal(Arch7bOperationalAuthorityReferenceKind.NonSecretEnvironment,
            taskkillReferences[0].ReferenceKind);
        var chromeReferences = inventory.References.Where(value =>
            value.AuthorityId == "chrome_executable").ToArray();
        Assert.Equal(3, chromeReferences.Length);
        Assert.Contains(chromeReferences, value => value.ReferencingStageId == "PORTAL_SESSION_PROVEN");
        Assert.Contains(chromeReferences, value => value.ReferencingStageId == "BRACKET_T2");
        Assert.Contains(chromeReferences, value => value.ReferenceKind ==
            Arch7bOperationalAuthorityReferenceKind.StaticPreSpawn);
        Assert.All(inventory.References, reference =>
            Assert.Equal(Arch7bOneShotContracts.Sha256(reference.Canonical()),
                reference.EvidenceSha256));
    }

    private static Arch7bOneShotLivePlanTemplate OperationalSkeleton(string root)
    {
        var fixture = Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, Path.Combine(root, "runtime"));
        var authorities = new Dictionary<string, Arch7bFileAuthority>(
            fixture.Template.FileAuthorities, StringComparer.Ordinal);
        foreach (var pair in Arch7bTaskkillTestAuthorities.Create())
            authorities[pair.Key] = pair.Value;
        var catalog = Arch7bOperationalLiveFactBindingCatalog.Build();
        var commands = fixture.Template.CommandTemplates
            .Where(command => Arch7bFinalStageExecutionCatalog.Require(command.StageId)
                .HasCommandTemplate)
            .Select(command =>
            {
                var entry = Arch7bFinalStageExecutionCatalog.Require(command.StageId);
                return command with
                {
                    CommandId = entry.CommandId!,
                    ExecutionKind = entry.ExecutionKind,
                    AdapterId = entry.AdapterId!,
                    ExpectedNativeOutputContract = entry.NativeContract!,
                    ArgumentTemplates = command.ArgumentTemplates.Select(argument =>
                        argument.Value == "fake-native-child"
                            ? argument with { Value = entry.Mode! }
                            : argument).ToArray(),
                    EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                        "classified-prototype:" + entry.StageId)
                };
            }).ToList();

        foreach (var commandCatalog in catalog)
        {
            var index = commands.FindIndex(value => value.StageId == commandCatalog.StageId);
            if (index < 0) continue;
            var prototype = commands[index];
            var arguments = new List<Arch7bCommandTemplateArgument>
            {
                new("--mode", Arch7bPlaceholderValueKind.Literal, null, -1, false),
                new(commandCatalog.Mode, Arch7bPlaceholderValueKind.Literal, null, -1, false)
            };
            foreach (var binding in commandCatalog.Bindings)
            {
                arguments.Add(new(binding.ArgumentName,
                    Arch7bPlaceholderValueKind.Literal, null, -1, false));
                arguments.Add(new(Arch7bOperationalLiveFactBindingCatalog.Marker,
                    Arch7bPlaceholderValueKind.Literal, null, -1, false));
            }
            commands[index] = prototype with
            {
                CommandId = commandCatalog.CommandId,
                ArgumentTemplates = arguments,
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                    "prototype:" + commandCatalog.CommandId)
            };
        }
        var provisional = fixture.Template with
        {
            FileAuthorities = authorities,
            CommandTemplates = commands,
            EvidenceSha256 = string.Empty
        };
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }

    private static string Sha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
