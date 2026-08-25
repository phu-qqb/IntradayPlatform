using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

/// <summary>
/// Creates the deterministic, non-operational source template which is subsequently
/// target-projected by the operational authority materializer.  It deliberately does
/// not select a slot, read a secret, or start any child process.
/// </summary>
public static class Arch7bGovernedSourceTemplateMaterializer
{
    public const string ContractVersion =
        "arch7b_governed_source_template_materializer_v1";

    public static async Task<object> WriteAsync(string supervisorExecutable, string dotnetRoot,
        string gitExecutable, string nodeExecutable, string taskkillExecutable,
        string chromeExecutable, string intradayCommit, string intradayTree,
        string runtimeInventorySha256, string outputPath,
        CancellationToken cancellationToken = default)
    {
        RequireCommit(intradayCommit, "intraday-commit");
        RequireCommit(intradayTree, "intraday-tree");
        if (!Arch7bOneShotContracts.IsSha256(runtimeInventorySha256))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, "runtime-inventory-sha256");

        supervisorExecutable = RequireFile(supervisorExecutable, "supervisor-executable");
        dotnetRoot = RequireDirectory(dotnetRoot, "dotnet-root");
        gitExecutable = RequireFile(gitExecutable, "git-executable");
        nodeExecutable = RequireFile(nodeExecutable, "node-executable");
        taskkillExecutable = RequireFile(taskkillExecutable, "taskkill-executable");
        chromeExecutable = RequireFile(chromeExecutable, "chrome-executable");
        outputPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputPath))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootReused, outputPath);

        var fixture = Arch7bV2QualificationFactory.Create(supervisorExecutable,
            Path.Combine(Path.GetTempPath(), "qq-arch7b-governed-source-template"),
            dotnetRoot: dotnetRoot);
        var authorities = new Dictionary<string, Arch7bFileAuthority>(
            fixture.Template.FileAuthorities, StringComparer.Ordinal)
        {
            ["git_executable"] = FileAuthority("git_executable", gitExecutable),
            ["node_executable"] = FileAuthority("node_executable", nodeExecutable),
            ["taskkill_executable"] = FileAuthority("taskkill_executable", taskkillExecutable),
            ["chrome_executable"] = FileAuthority("chrome_executable", chromeExecutable)
        };
        var skeleton = CreateOperationalSkeleton(fixture.Template, authorities) with
        {
            SupervisorCommit = intradayCommit,
            SupervisorTree = intradayTree,
            IntradayCommit = intradayCommit,
            IntradayTree = intradayTree,
            RuntimeInventorySha256 = runtimeInventorySha256,
            FileAuthorities = authorities,
            EvidenceSha256 = string.Empty
        };
        skeleton = skeleton with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(skeleton.Canonical())
        };
        var sourceManifestPath = Path.Combine(RepositoryRoot(), "docs", "architecture",
            "arch7b", "arch7b-position-market-live-command-manifest.json");
        var materialized = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(skeleton,
            await File.ReadAllBytesAsync(sourceManifestPath, cancellationToken).ConfigureAwait(false));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(materialized.Template,
            Arch7bJson.CanonicalOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write,
                         FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        _ = await Arch7bSourceTemplateProvenanceValidator.ValidateAsync(outputPath, sha,
            intradayCommit, intradayTree, cancellationToken).ConfigureAwait(false);
        return new
        {
            verdict = "ARCH7B_GOVERNED_SOURCE_TEMPLATE_MATERIALIZED",
            qualificationOnly = true,
            sourceTemplatePath = outputPath,
            sourceTemplateSha256 = sha,
            stageCount = materialized.Template.StageContracts.Count,
            commandCount = materialized.Template.CommandTemplates.Count,
            runtimeInventorySha256,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    private static Arch7bFileAuthority FileAuthority(string id, string path) => new(id, path,
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), true, false);

    private static Arch7bOneShotLivePlanTemplate CreateOperationalSkeleton(
        Arch7bOneShotLivePlanTemplate template,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var classification = Arch7bFinalStageExecutionCatalog.All;
        var commandStages = classification.Where(value => value.HasCommandTemplate)
            .Select(value => value.StageId).ToHashSet(StringComparer.Ordinal);
        var commands = template.CommandTemplates
            .Where(command => commandStages.Contains(command.StageId))
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
        foreach (var catalog in Arch7bOperationalLiveFactBindingCatalog.Build())
        {
            var index = commands.FindIndex(command => command.StageId == catalog.StageId);
            if (index < 0) continue;
            var prototype = commands[index];
            var arguments = new List<Arch7bCommandTemplateArgument>
            {
                new("--mode", Arch7bPlaceholderValueKind.Literal, null, -1, false),
                new(catalog.Mode, Arch7bPlaceholderValueKind.Literal, null, -1, false)
            };
            foreach (var binding in catalog.Bindings)
            {
                arguments.Add(new(binding.ArgumentName, Arch7bPlaceholderValueKind.Literal,
                    null, -1, false));
                arguments.Add(new(Arch7bOperationalLiveFactBindingCatalog.Marker,
                    Arch7bPlaceholderValueKind.Literal, null, -1, false));
            }
            commands[index] = prototype with
            {
                CommandId = catalog.CommandId,
                ArgumentTemplates = arguments,
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                    "prototype:" + catalog.CommandId)
            };
        }
        var provisional = template with
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

    private static string RequireFile(string value, string id)
    {
        value = Path.GetFullPath(value);
        return File.Exists(value) ? value : throw new Arch7bQualificationException(
            Arch7bV2Blockers.AuthorityBindingMismatch, id);
    }

    private static string RequireDirectory(string value, string id)
    {
        value = Path.GetFullPath(value);
        return Directory.Exists(value) ? value : throw new Arch7bQualificationException(
            Arch7bV2Blockers.AuthorityBindingMismatch, id);
    }

    private static void RequireCommit(string value, string id)
    {
        if (value.Length != 40 || value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, id);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "architecture", "arch7b",
                "arch7b-position-market-live-command-manifest.json");
            if (File.Exists(candidate)) return directory.FullName;
            directory = directory.Parent;
        }
        throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch,
            "source-command-manifest");
    }
}
