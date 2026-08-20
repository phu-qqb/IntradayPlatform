using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOperationalLiveFactBindingCatalogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-binding-catalog", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Catalog_contains_exactly_six_commands_and_34_bindings()
    {
        var document = Arch7bOperationalLiveFactBindingCatalog.Document();

        Assert.Equal(6, document.CommandCount);
        Assert.Equal(34, document.BindingCount);
        Assert.Equal(6, document.Commands.Select(value => value.CommandId)
            .Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Marker_inventory_is_bijective_with_catalog()
    {
        var inventory = Inventory();
        var bindings = Arch7bOperationalLiveFactBindingCatalog.Build()
            .SelectMany(value => value.Bindings).ToArray();

        Assert.Equal(34, inventory.MarkerCount);
        Assert.Equal(bindings.Select(value =>
                (value.CommandId, value.ArgumentIndex, value.ArgumentName)).Order().ToArray(),
            inventory.Markers.Select(value =>
                (value.CommandId, value.ArgumentIndex, value.ArgumentName)).Order().ToArray());
    }

    [Fact]
    public void Every_binding_id_and_command_index_is_unique()
    {
        var bindings = Bindings();

        Assert.Equal(bindings.Length,
            bindings.Select(value => value.BindingId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(bindings.Length,
            bindings.Select(value => (value.CommandId, value.ArgumentIndex)).Distinct().Count());
    }

    [Fact]
    public void Every_fact_or_artifact_binding_has_real_existing_producer_stage()
    {
        var bindings = Bindings().Where(value =>
            value.PlaceholderScope != Arch7bOperationalPlaceholderScope.Authority);

        Assert.All(bindings, binding =>
        {
            Assert.NotNull(binding.RequiredProducerStage);
            Assert.Contains(binding.RequiredProducerStage!, Arch7bStages.All);
        });
    }

    [Fact]
    public void Authority_bindings_are_static_and_have_no_runtime_producer()
    {
        var bindings = Bindings().Where(value =>
            value.PlaceholderScope == Arch7bOperationalPlaceholderScope.Authority).ToArray();

        Assert.NotEmpty(bindings);
        Assert.All(bindings, binding =>
        {
            Assert.Null(binding.RequiredProducerStage);
            Assert.Equal(-1, binding.MaximumAgeSeconds);
            Assert.False(binding.MustBeInsideRunRoot);
        });
    }

    [Fact]
    public void Every_one_shot_artifact_path_is_confined_to_run_root()
    {
        var paths = Bindings().Where(value =>
            value.ValueKind == Arch7bPlaceholderValueKind.AbsolutePath &&
            value.PlaceholderScope != Arch7bOperationalPlaceholderScope.Authority).ToArray();

        Assert.NotEmpty(paths);
        Assert.All(paths, binding => Assert.True(binding.MustBeInsideRunRoot));
    }

    [Fact]
    public void Expected_draft_sha_comes_only_from_post_creation_artifact()
    {
        var bindings = Bindings().Where(value =>
            value.ArgumentName == "--expected-position-market-draft-sha256").ToArray();

        Assert.Equal(3, bindings.Length);
        Assert.All(bindings, binding =>
        {
            Assert.Equal(Arch7bOperationalPlaceholderScope.Artifact,
                binding.PlaceholderScope);
            Assert.Equal("position_market_draft_artifact", binding.PlaceholderName);
            Assert.Equal("sha256", binding.PlaceholderField);
            Assert.Equal("POSITION_MARKET_DRAFT", binding.RequiredProducerStage);
        });
    }

    [Fact]
    public void Draft_output_path_binding_is_exact()
    {
        var binding = Bindings().Single(value =>
            value.CommandId == "prearmed-importer" &&
            value.ArgumentName == "--position-market-draft-path");

        Assert.Equal(Arch7bOperationalPlaceholderScope.Fact, binding.PlaceholderScope);
        Assert.Equal("position_market_draft_output_path", binding.PlaceholderName);
        Assert.Equal("path", binding.PlaceholderField);
        Assert.Equal(Arch7bPlaceholderValueKind.AbsolutePath, binding.ValueKind);
        Assert.Equal("ONE_SHOT_IDENTITIES_CREATED", binding.RequiredProducerStage);
        Assert.Equal(-1, binding.MaximumAgeSeconds);
        Assert.True(binding.MustBeInsideRunRoot);
        Assert.True(binding.Required);
    }

    [Fact]
    public async Task Generated_catalog_and_inventory_are_byte_deterministic()
    {
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        await Arch7bOperationalCatalogMaterializer.MaterializeAsync(
            SourceManifestPath(), first);
        await Arch7bOperationalCatalogMaterializer.MaterializeAsync(
            SourceManifestPath(), second);

        Assert.Equal(File.ReadAllBytes(Path.Combine(first,
                Arch7bOperationalCatalogMaterializer.CatalogFileName)),
            File.ReadAllBytes(Path.Combine(second,
                Arch7bOperationalCatalogMaterializer.CatalogFileName)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(first,
                Arch7bOperationalCatalogMaterializer.MarkerInventoryFileName)),
            File.ReadAllBytes(Path.Combine(second,
                Arch7bOperationalCatalogMaterializer.MarkerInventoryFileName)));
    }

    [Fact]
    public async Task Versioned_json_is_generated_from_current_code()
    {
        await Arch7bOperationalCatalogMaterializer.MaterializeAsync(
            SourceManifestPath(), root);

        Assert.Equal(File.ReadAllBytes(Path.Combine(root,
                Arch7bOperationalCatalogMaterializer.CatalogFileName)),
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "docs", "architecture",
                "arch7b", Arch7bOperationalCatalogMaterializer.CatalogFileName)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(root,
                Arch7bOperationalCatalogMaterializer.MarkerInventoryFileName)),
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "docs", "architecture",
                "arch7b", Arch7bOperationalCatalogMaterializer.MarkerInventoryFileName)));
    }

    [Fact]
    public void Added_marker_without_binding_is_rejected()
    {
        var rootNode = JsonNode.Parse(File.ReadAllBytes(SourceManifestPath()))!.AsObject();
        var command = rootNode["commands"]!.AsArray()[0]!.AsObject();
        command["arguments"]!.AsObject()["--unbound-live-value"] =
            Arch7bOperationalLiveFactBindingCatalog.Marker;

        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalLiveFactBindingCatalog.InventoryMarkers(
                Arch7bOperationalCatalogMaterializer.SourceManifestLabel,
                JsonSerializer.SerializeToUtf8Bytes(rootNode)));
    }

    [Fact]
    public void Removed_marker_with_remaining_binding_is_rejected()
    {
        var rootNode = JsonNode.Parse(File.ReadAllBytes(SourceManifestPath()))!.AsObject();
        var command = rootNode["commands"]!.AsArray()[0]!.AsObject();
        command["arguments"]!.AsObject()["--position-market-draft-path"] =
            "static-value-is-not-a-marker";

        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalLiveFactBindingCatalog.InventoryMarkers(
                Arch7bOperationalCatalogMaterializer.SourceManifestLabel,
                JsonSerializer.SerializeToUtf8Bytes(rootNode)));
    }

    [Fact]
    public void Catalog_contains_no_regenerate_marker_or_fake_child()
    {
        var json = JsonSerializer.Serialize(
            Arch7bOperationalLiveFactBindingCatalog.Document());

        Assert.DoesNotContain(Arch7bOperationalLiveFactBindingCatalog.Marker, json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("fake-native-child", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_materializer_replaces_every_marker_with_typed_placeholder()
    {
        var result = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            OperationalSkeleton(), File.ReadAllBytes(SourceManifestPath()));
        var text = JsonSerializer.Serialize(result.Template);

        Assert.Equal(Arch7bFinalStageExecutionCatalog.CommandTemplateCount,
            result.CommandCount);
        Assert.Equal(13, result.CommandCount);
        Assert.Equal(34, result.BindingCount);
        Assert.Equal(0, result.UnresolvedBindingCount);
        Assert.Equal(0, result.SyntheticCommandCount);
        Assert.DoesNotContain(Arch7bOperationalLiveFactBindingCatalog.Marker, text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("fake-native-child", text, StringComparison.Ordinal);
        Assert.Contains("$" + "{fact:position_market_draft_output_path.path}", text,
            StringComparison.Ordinal);
        Assert.Contains("$" + "{artifact:position_market_draft_artifact.sha256}", text,
            StringComparison.Ordinal);
        var core = result.Template.CommandTemplates.Single(value =>
            value.StageId == "CORE_PREQUALIFICATION");
        Assert.Equal("node_executable", core.ExecutableAuthorityId);
        Assert.Equal("core_node_runtime", core.WorkingDirectoryAuthorityId);
        Assert.Equal(new[]
        {
            "src/fast-seal-cli.mjs",
            "prequalify-bracket-runtime",
            "--config",
            "$" + "{fact:core_prequalification_config.path}"
        }, core.ArgumentTemplates.Select(value => value.Value));
        Assert.DoesNotContain(core.ArgumentTemplates,
            value => value.Value.Contains("npm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(core.ArgumentTemplates,
            value => value.Value.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                     value.Value.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Single(core.NonSecretEnvironment);
        var executablePath = core.NonSecretEnvironment.Single(value =>
            value.VariableName == "PATH");
        Assert.Equal("PATH", executablePath.VariableName);
        Assert.Equal(Arch7bSealedNonSecretEnvironment.CorePrequalificationPathAuthorityId,
            executablePath.SourceAuthorityId);
        Assert.Equal(Arch7bNonSecretEnvironmentValueKind.ExecutableSearchPath,
            executablePath.ValueKind);
        Assert.Equal(string.Join(Path.PathSeparator, new[]
        {
            Path.GetDirectoryName(result.Template.FileAuthorities["git_executable"].Path),
            Path.GetDirectoryName(result.Template.FileAuthorities["node_executable"].Path),
            Path.GetDirectoryName(result.Template.FileAuthorities["taskkill_executable"].Path)
        }),
            executablePath.Value);
        Assert.DoesNotContain(core.NonSecretEnvironment,
            value => value.VariableName == "ProgramFiles(x86)");
        Assert.DoesNotContain(core.NonSecretEnvironment,
            value => value.VariableName == "DOTNET_ROOT");
        var apphosts = result.Template.CommandTemplates.Where(command =>
            command.ExecutableAuthorityId == "supervisor_executable").ToArray();
        Assert.Equal(12, apphosts.Length);
        Assert.Equal(new[]
        {
            "PORTAL_SESSION_PROVEN", "RDS_READ_1", "BRACKET_T2", "CORE_FAST_SEAL",
            "POSITION_APPLY", "RUNTIME_SELECTION", "CLOCK_CAPTURE_START", "MARKET_CAPTURE",
            "MARKET_FINALIZATION", "PMS_IMPORT", "ARCH7A_QUALIFY_SHADOW", "REPORTING"
        }, apphosts.Select(command => command.StageId));
        var expectedDotnetRoot = Arch7bSealedNonSecretEnvironment
            .ForDotnetRoot(result.Template.FileAuthorities).Single();
        Assert.All(apphosts, command =>
        {
            var variable = Assert.Single(command.NonSecretEnvironment);
            Assert.Equal(expectedDotnetRoot, variable);
            Assert.Equal("DOTNET_ROOT", variable.VariableName);
            Assert.Equal("dotnet_root", variable.SourceAuthorityId);
            Assert.DoesNotContain(command.NonSecretEnvironment,
                value => value.VariableName == "PATH");
        });
        Assert.DoesNotContain(result.Template.CommandTemplates,
            command => command.StageId == "PRELOADED_LEASE_READY");
        Assert.True(Arch7bTargetCommandEnvironmentValidator.Validate(result.Template).Passed);
        Assert.Equal("CHROME_EXPLICIT_EXECUTABLE", result.Template.SelectedBrowser);
        Assert.True(result.Template.FileAuthorities.ContainsKey("chrome_executable"));
        Assert.False(result.Template.FileAuthorities.ContainsKey("msedge_executable"));
        foreach (var stage in new[] { "PORTAL_SESSION_PROVEN", "BRACKET_T2" })
            Assert.Contains(result.Template.CommandTemplates.Single(value =>
                    value.StageId == stage).ArgumentTemplates,
                value => value.Value == "${authority:chrome_executable.path}");
        Assert.Contains("core_prequalification_config",
            result.Template.StageContracts.Single(value =>
                value.StageId == "SLOT_LOCKED").ProducedFactTypes);
        Assert.Contains("core_prequalification_config",
            result.Template.StageContracts.Single(value =>
                value.StageId == "CORE_PREQUALIFICATION").RequiredFactTypes);
    }

    [Fact]
    public void Target_validation_rejects_v7r11_apphost_without_dotnet_root()
    {
        var template = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            OperationalSkeleton(), File.ReadAllBytes(SourceManifestPath())).Template;
        var commands = template.CommandTemplates.Select(command =>
            command.StageId == "PORTAL_SESSION_PROVEN"
                ? command with { NonSecretEnvironment = [] }
                : command).ToArray();
        var stale = template with { CommandTemplates = commands };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetCommandEnvironmentValidator.Validate(stale));

        Assert.Equal(Arch7bV2Blockers.ApphostDotnetRootBindingMissing, error.BlockerCode);
    }

    [Fact]
    public void Target_validation_rejects_path_on_apphost()
    {
        var template = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            OperationalSkeleton(), File.ReadAllBytes(SourceManifestPath())).Template;
        var corePath = template.CommandTemplates.Single(command =>
            command.StageId == "CORE_PREQUALIFICATION").NonSecretEnvironment;
        var commands = template.CommandTemplates.Select(command =>
            command.StageId == "PORTAL_SESSION_PROVEN"
                ? command with { NonSecretEnvironment = corePath }
                : command).ToArray();
        var stale = template with { CommandTemplates = commands };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetCommandEnvironmentValidator.Validate(stale));

        Assert.Equal(Arch7bV2Blockers.ApphostDotnetRootBindingMissing, error.BlockerCode);
    }

    [Fact]
    public void Operational_template_materialization_is_deterministic()
    {
        var skeleton = OperationalSkeleton();
        var source = File.ReadAllBytes(SourceManifestPath());
        var first = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            skeleton, source);
        var second = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            skeleton, source);

        Assert.Equal(first.EvidenceSha256, second.EvidenceSha256);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first.Template),
            JsonSerializer.SerializeToUtf8Bytes(second.Template));
    }

    [Fact]
    public void Operational_template_declares_each_catalog_producer_fact()
    {
        var template = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            OperationalSkeleton(), File.ReadAllBytes(SourceManifestPath())).Template;

        foreach (var binding in Bindings().Where(value =>
                     value.PlaceholderScope != Arch7bOperationalPlaceholderScope.Authority))
        {
            var producer = template.StageContracts.Single(value =>
                value.StageId == binding.RequiredProducerStage);
            Assert.Contains(binding.PlaceholderName, producer.ProducedFactTypes);
            if (binding.RequiredProducerStage != binding.StageId)
                Assert.Contains(binding.PlaceholderName,
                    template.StageContracts.Single(value =>
                        value.StageId == binding.StageId).RequiredFactTypes);
        }
    }

    [Fact]
    public async Task Producer_audit_classifies_all_bindings_and_is_versioned_byte_deterministically()
    {
        var audit = Arch7bOperationalBindingProducerAudit.Build();

        Assert.Equal(34, audit.BindingCount);
        Assert.Equal(0, audit.MissingProducerCount);
        Assert.DoesNotContain(audit.Bindings, value =>
            value.Classification ==
            Arch7bOperationalBindingProducerClassifications.RealProducerMissing);
        Assert.Contains(audit.Bindings, value => value.Classification ==
            Arch7bOperationalBindingProducerClassifications.StaticAuthorityExists);
        Assert.Contains(audit.Bindings, value => value.Classification ==
            Arch7bOperationalBindingProducerClassifications.FactProducerExists);
        Assert.Contains(audit.Bindings, value => value.Classification ==
            Arch7bOperationalBindingProducerClassifications
                .DeterministicRunOutputPathProducerExists);
        Assert.Contains(audit.Bindings, value => value.Classification ==
            Arch7bOperationalBindingProducerClassifications
                .ArtifactProducerAndValidationGateExist);

        var first = Path.Combine(root, "audit-first");
        var second = Path.Combine(root, "audit-second");
        await Arch7bOperationalCatalogMaterializer.MaterializeAsync(
            SourceManifestPath(), first);
        await Arch7bOperationalCatalogMaterializer.MaterializeAsync(
            SourceManifestPath(), second);
        var firstBytes = File.ReadAllBytes(Path.Combine(first,
            Arch7bOperationalCatalogMaterializer.ProducerAuditFileName));
        Assert.Equal(firstBytes, File.ReadAllBytes(Path.Combine(second,
            Arch7bOperationalCatalogMaterializer.ProducerAuditFileName)));
        Assert.Equal(firstBytes, File.ReadAllBytes(Path.Combine(RepositoryRoot(),
            "docs", "architecture", "arch7b",
            Arch7bOperationalCatalogMaterializer.ProducerAuditFileName)));
    }

    [Fact]
    public async Task Operational_template_file_is_create_new_and_byte_deterministic()
    {
        var sourceTemplate = Path.Combine(root, "source-template.json");
        var firstPath = Path.Combine(root, "first", "operational-template.json");
        var secondPath = Path.Combine(root, "second", "operational-template.json");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(sourceTemplate, JsonSerializer.SerializeToUtf8Bytes(
            OperationalSkeleton(), Arch7bJson.CanonicalOptions));

        var first = await Arch7bOperationalLivePlanTemplateMaterializer.WriteAsync(
            sourceTemplate, SourceManifestPath(), firstPath);
        var second = await Arch7bOperationalLivePlanTemplateMaterializer.WriteAsync(
            sourceTemplate, SourceManifestPath(), secondPath);

        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        Assert.Equal(first.OutputSha256, second.OutputSha256);
        Assert.Equal(File.ReadAllBytes(first.StageFactInventoryPath),
            File.ReadAllBytes(second.StageFactInventoryPath));
        Assert.Equal(first.StageFactInventorySha256,
            second.StageFactInventorySha256);
        Assert.Equal(Arch7bFinalStageExecutionCatalog.CommandTemplateCount,
            first.CommandCount);
        Assert.Equal(34, first.BindingCount);
        Assert.Equal(0, first.RegenerateCount);
        Assert.Equal(0, first.FakeNativeChildCount);
        Assert.Equal(0, first.SyntheticAuthorityCount);
        Assert.Equal(0, first.UnresolvedProducerCount);
        Assert.True(first.ReadbackIdentical);
        await Assert.ThrowsAnyAsync<IOException>(() =>
            Arch7bOperationalLivePlanTemplateMaterializer.WriteAsync(
                sourceTemplate, SourceManifestPath(), firstPath));
    }

    [Fact]
    public void Operational_materializer_replaces_all_three_legacy_clock_requirements()
    {
        var skeleton = OperationalSkeleton();
        var stages = skeleton.StageContracts.Select(stage =>
        {
            var contract = Arch7bClockFactContracts.All.SingleOrDefault(value =>
                value.ConsumerStage == stage.StageId);
            if (contract is null) return stage;
            return Rehash(stage with
            {
                RequiredFactTypes = stage.RequiredFactTypes
                    .Where(value => value != contract.FactType)
                    .Append(contract.LegacyAlias).ToArray()
            });
        }).ToArray();
        skeleton = Rehash(skeleton with { StageContracts = stages });

        var result = Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
            skeleton, File.ReadAllBytes(SourceManifestPath()));

        Assert.Equal("PASS", result.StageFactGraph.ValidationStatus);
        Assert.Equal(0, result.StageFactGraph.LegacyAliasCount);
        foreach (var contract in Arch7bClockFactContracts.All)
        {
            var consumer = result.Template.StageContracts.Single(value =>
                value.StageId == contract.ConsumerStage);
            Assert.Contains(contract.FactType, consumer.RequiredFactTypes);
            Assert.DoesNotContain(contract.LegacyAlias, consumer.RequiredFactTypes);
        }
    }

    [Fact]
    public void Operational_materializer_rejects_legacy_alias_outside_its_consumer()
    {
        var skeleton = OperationalSkeleton();
        var stages = skeleton.StageContracts.Select(stage => stage.StageId == "REPORTING"
            ? Rehash(stage with
            {
                RequiredFactTypes = stage.RequiredFactTypes.Append(
                    Arch7bClockFactContracts.LegacyPreflightFactType).ToArray()
            })
            : stage).ToArray();
        skeleton = Rehash(skeleton with { StageContracts = stages });

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalLivePlanTemplateMaterializer.Materialize(
                skeleton, File.ReadAllBytes(SourceManifestPath())));

        Assert.Equal(Arch7bV2Blockers.LegacyStageFactAliasPresent,
            error.BlockerCode);
    }

    private Arch7bOneShotLivePlanTemplate OperationalSkeleton()
    {
        var fixture = Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, Path.Combine(root, "runtime"), dotnetRoot: DotnetRoot());
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
            var index = commands.FindIndex(value =>
                value.StageId == commandCatalog.StageId);
            Arch7bOneShotCommandTemplate prototype;
            if (index >= 0)
                prototype = commands[index];
            else
                continue;
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

    private static Arch7bOneShotStageContract Rehash(
        Arch7bOneShotStageContract stage)
    {
        var provisional = stage with { EvidenceSha256 = string.Empty };
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(string.Join('\n',
                provisional.StageId, provisional.ExecutionKind,
                string.Join('|', provisional.Predecessors),
                string.Join('|', provisional.RequiredFactTypes),
                string.Join('|', provisional.ProducedFactTypes),
                provisional.SloId ?? string.Empty, provisional.ValidatorId))
        };
    }

    private static Arch7bOneShotLivePlanTemplate Rehash(
        Arch7bOneShotLivePlanTemplate template)
    {
        var provisional = template with { EvidenceSha256 = string.Empty };
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
    }

    private static Arch7bOperationalLiveFactBinding[] Bindings() =>
        Arch7bOperationalLiveFactBindingCatalog.Build()
            .SelectMany(value => value.Bindings).ToArray();

    private static Arch7bV7CommandMarkerInventory Inventory() =>
        Arch7bOperationalLiveFactBindingCatalog.InventoryMarkers(
            Arch7bOperationalCatalogMaterializer.SourceManifestLabel,
            File.ReadAllBytes(SourceManifestPath()));

    private static string SourceManifestPath() => Path.Combine(RepositoryRoot(), "docs",
        "architecture", "arch7b", "arch7b-position-market-live-command-manifest.json");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static string DotnetRoot()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(Path.Combine(configured, "dotnet.exe")))
            return Path.GetFullPath(configured);
        var installed = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (!File.Exists(Path.Combine(installed, "dotnet.exe")))
            throw new DirectoryNotFoundException(installed);
        return installed;
    }

    private static string Sha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
