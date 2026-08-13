using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCorePrequalificationConfigContractTests : IDisposable
{
    private readonly string root;
    private readonly string repositoryRoot;
    private readonly string coreNodeRoot;
    private readonly string runRoot;
    private readonly string chromePath;
    private readonly string chromeSha;
    private readonly string commit = new('a', 40);
    private readonly string tree = new('b', 40);

    public Arch7bCorePrequalificationConfigContractTests()
    {
        root = Path.Combine(Path.GetTempPath(), "qq-arch7b-core-prequal-config",
            Guid.NewGuid().ToString("N"));
        repositoryRoot = Path.Combine(root, "core-repository");
        coreNodeRoot = Path.Combine(root, "core-node-runtime");
        runRoot = Path.Combine(root, "run");
        chromePath = Path.Combine(root, "chrome.exe");
        Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
        Directory.CreateDirectory(coreNodeRoot);
        Directory.CreateDirectory(runRoot);
        File.WriteAllBytes(chromePath, "qualified-chrome"u8.ToArray());
        chromeSha = Sha(File.ReadAllBytes(chromePath));
    }

    [Fact]
    public void Serializer_emits_exact_six_camel_case_properties_in_canonical_order()
    {
        using var document = JsonDocument.Parse(ValidBytes());

        Assert.Equal(new[]
        {
            "repositoryRoot", "outputRoot", "expectedCommit", "expectedTree",
            "browserExecutablePath", "expectedBrowserExecutableSha256"
        }, document.RootElement.EnumerateObject().Select(value => value.Name));
    }

    [Fact]
    public void Serializer_is_byte_for_byte_deterministic()
    {
        Assert.Equal(ValidBytes(), ValidBytes());
    }

    [Fact]
    public void Contract_version_is_explicit()
    {
        Assert.Equal("arch7b_core_prequalification_config_v1",
            Arch7bCorePrequalificationConfigV1.ContractVersion);
    }

    [Fact]
    public void Exact_config_is_accepted()
    {
        var actual = Parse(ValidBytes());

        Assert.Equal(repositoryRoot, actual.RepositoryRoot);
        Assert.Equal(coreNodeRoot, Context().CoreNodeRuntimeRoot);
        Assert.NotEqual(actual.RepositoryRoot, Context().CoreNodeRuntimeRoot);
    }

    [Theory]
    [InlineData("repositoryRoot", "repository_root")]
    [InlineData("browserExecutablePath", "browser_executable_path")]
    [InlineData("repositoryRoot", "RepositoryRoot")]
    [InlineData("browserExecutablePath", "BrowserExecutablePath")]
    public void Wrong_property_casing_is_rejected(string expected, string replacement)
    {
        AssertBlocker(ReplaceName(expected, replacement),
            Arch7bV2Blockers.CorePrequalificationConfigNamingMismatch);
    }

    [Fact]
    public void Extra_property_is_rejected()
    {
        AssertBlocker(InsertBeforeClose(",\"extra\":\"value\""),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Missing_property_is_rejected()
    {
        var json = Encoding.UTF8.GetString(ValidBytes());
        var suffix = ",\"expectedBrowserExecutableSha256\":" +
                     JsonSerializer.Serialize(chromeSha);
        AssertBlocker(Encoding.UTF8.GetBytes(json.Replace(suffix, string.Empty,
                StringComparison.Ordinal)),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Duplicate_property_is_rejected()
    {
        var json = Encoding.UTF8.GetString(ValidBytes());
        var duplicate = "\"repositoryRoot\":" +
                        JsonSerializer.Serialize(repositoryRoot) + ",";
        AssertBlocker(Encoding.UTF8.GetBytes(json.Insert(1, duplicate)),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Browser_channel_is_rejected()
    {
        AssertBlocker(InsertBeforeClose(",\"browserChannel\":\"chrome\""),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Utf8_bom_is_rejected()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(ValidBytes()).ToArray();
        AssertBlocker(bytes, Arch7bV2Blockers.CorePrequalificationConfigNamingMismatch);
    }

    [Fact]
    public void Trailing_comma_is_rejected()
    {
        AssertBlocker(InsertBeforeClose(","),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Comment_is_rejected()
    {
        var json = Encoding.UTF8.GetString(ValidBytes()).Insert(1, "/*comment*/");
        AssertBlocker(Encoding.UTF8.GetBytes(json),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Null_value_is_rejected()
    {
        var json = Encoding.UTF8.GetString(ValidBytes());
        var quoted = JsonSerializer.Serialize(repositoryRoot);
        AssertBlocker(Encoding.UTF8.GetBytes(json.Replace(quoted, "null",
                StringComparison.Ordinal)),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    [Fact]
    public void Core_repository_authority_is_accepted()
    {
        Assert.Equal(repositoryRoot, Parse(ValidBytes()).RepositoryRoot);
    }

    [Fact]
    public void Core_node_runtime_as_repository_is_rejected()
    {
        var config = ValidConfig() with { RepositoryRoot = coreNodeRoot };
        AssertBlocker(Serialize(config),
            Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch);
    }

    [Fact]
    public void Wrong_core_commit_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with { ExpectedCommit = new string('c', 40) }),
            Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch);
    }

    [Fact]
    public void Wrong_core_tree_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with { ExpectedTree = new string('c', 40) }),
            Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch);
    }

    [Fact]
    public void Abbreviated_core_commit_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with { ExpectedCommit = "abcdef1" }),
            Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch);
    }

    [Fact]
    public void Output_root_outside_run_root_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with
            {
                OutputRoot = Path.Combine(root, "outside")
            }), Arch7bV2Blockers.CorePrequalificationConfigOutputRootInvalid);
    }

    [Fact]
    public void Preexisting_output_root_is_rejected()
    {
        Directory.CreateDirectory(ValidConfig().OutputRoot);

        AssertBlocker(ValidBytes(),
            Arch7bV2Blockers.CorePrequalificationConfigOutputRootInvalid);
    }

    [Fact]
    public void Missing_output_root_is_accepted()
    {
        Assert.False(Directory.Exists(ValidConfig().OutputRoot));
        _ = Parse(ValidBytes());
    }

    [Fact]
    public void Wrong_browser_path_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with
            {
                BrowserExecutablePath = Path.Combine(root, "other-chrome.exe")
            }), Arch7bV2Blockers.CorePrequalificationConfigBrowserAuthorityMismatch);
    }

    [Fact]
    public void Wrong_browser_sha_is_rejected()
    {
        AssertBlocker(Serialize(ValidConfig() with
            {
                ExpectedBrowserExecutableSha256 = new string('c', 64)
            }), Arch7bV2Blockers.CorePrequalificationConfigBrowserAuthorityMismatch);
    }

    [Fact]
    public void Browser_content_sha_mismatch_is_rejected()
    {
        var bytes = ValidBytes();
        File.WriteAllBytes(chromePath, "changed-chrome"u8.ToArray());

        AssertBlocker(bytes,
            Arch7bV2Blockers.CorePrequalificationConfigBrowserAuthorityMismatch);
    }

    [Fact]
    public void Preslot_and_postlock_bytes_and_sha_are_identical()
    {
        var preSlot = ValidBytes();
        var path = Path.Combine(runRoot, "core-prequalification-config.json");
        File.WriteAllBytes(path, preSlot);
        var postLock = File.ReadAllBytes(path);

        Assert.Equal(preSlot, postLock);
        Assert.Equal(Sha(preSlot), Sha(postLock));
    }

    [Fact]
    public void Invalid_config_blocker_is_not_child_output_invalid()
    {
        var error = AssertBlocker(ReplaceName("repositoryRoot", "repository_root"),
            Arch7bV2Blockers.CorePrequalificationConfigNamingMismatch);

        Assert.NotEqual(Arch7bBlockers.ChildOutputInvalid, error.BlockerCode);
    }

    [Fact]
    public async Task Invalid_operational_config_fails_before_calendar_without_selecting_or_locking_slot()
    {
        var runtimeRunRoot = Path.Combine(root, "invalid-operational-run");
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), runtimeRunRoot);
        var authorities = new Dictionary<string, Arch7bFileAuthority>(
            fixture.Template.FileAuthorities, StringComparer.Ordinal)
        {
            ["core_repository"] = new("core_repository", coreNodeRoot,
                Arch7bOneShotContracts.Sha256("core-repository:" + coreNodeRoot), true, false),
            ["core_node_runtime"] = new("core_node_runtime", coreNodeRoot,
                Arch7bOneShotContracts.Sha256("core-node-runtime:" + coreNodeRoot), true, false),
            ["chrome_executable"] = new("chrome_executable", chromePath, chromeSha, true, false)
        };
        var stages = fixture.Template.StageContracts.Select(stage => stage.StageId switch
        {
            "SLOT_LOCKED" => Rehash(stage with
            {
                ProducedFactTypes = stage.ProducedFactTypes
                    .Append("core_prequalification_config").ToArray()
            }),
            "CORE_PREQUALIFICATION" => Rehash(stage with
            {
                RequiredFactTypes = stage.RequiredFactTypes
                    .Append("core_prequalification_config").ToArray()
            }),
            _ => stage
        }).ToArray();
        var template = fixture.Template with
        {
            FileAuthorities = authorities,
            StageContracts = stages,
            EvidenceSha256 = string.Empty
        };
        template = template with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(template.Canonical())
        };
        var templateBytes = JsonSerializer.SerializeToUtf8Bytes(template,
            Arch7bJson.CanonicalOptions);
        var templateSha = Sha(templateBytes);
        var authority = fixture.Authority with
        {
            FileAuthorities = authorities,
            LivePlanTemplateSha256 = templateSha,
            EvidenceSha256 = string.Empty
        };
        authority = authority with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(authority.Canonical())
        };
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);

        var result = await runtime.RunAsync(template, authority,
            fixture.OperatorAuthorization, templateSha, runtimeRunRoot,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease());

        Assert.Equal(Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch,
            result.FinalBlocker);
        Assert.Empty(result.Stages);
        Assert.DoesNotContain(result.Stages, stage => stage.StageId == "CALENDAR_LOADED");
        Assert.Equal(string.Empty, result.SlotId);
        Assert.Equal(0, result.Budget.Slots);
        Assert.False(File.Exists(Path.Combine(runtimeRunRoot, "selected-slot.json")));
        Assert.False(File.Exists(Path.Combine(runtimeRunRoot, "slot-lock.json")));
        Assert.NotEqual(Arch7bBlockers.ChildOutputInvalid, result.FinalBlocker);
    }

    [Fact]
    public async Task Prespawn_config_divergence_writes_failure_evidence_without_child_receipt()
    {
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(),
            Path.Combine(root, "unused-runtime-run"));
        var authorities = new Dictionary<string, Arch7bFileAuthority>(
            fixture.Template.FileAuthorities, StringComparer.Ordinal)
        {
            ["core_repository"] = new("core_repository", repositoryRoot,
                Arch7bOneShotContracts.Sha256("core-repository:" + repositoryRoot), true, false),
            ["core_node_runtime"] = new("core_node_runtime", coreNodeRoot,
                Arch7bOneShotContracts.Sha256("core-node-runtime:" + coreNodeRoot), true, false),
            ["chrome_executable"] = new("chrome_executable", chromePath, chromeSha, true, false)
        };
        var template = fixture.Template with
        {
            CoreCommit = commit,
            CoreTree = tree,
            FileAuthorities = authorities
        };
        Directory.CreateDirectory(Path.Combine(coreNodeRoot, "src"));
        File.WriteAllText(Path.Combine(coreNodeRoot, "src", "fast-seal-cli.mjs"),
            "// qualified module");
        var configPath = Path.Combine(runRoot, "core-prequalification-config.json");
        var validBytes = ValidBytes();
        File.WriteAllBytes(configPath,
            ReplaceName("repositoryRoot", "repository_root"));
        var authorityRoot = Path.Combine(runRoot, "command-authority");
        Directory.CreateDirectory(authorityRoot);
        var command = new Arch7bOneShotMaterializedCommand(
            Arch7bV2Contracts.MaterializedCommandVersion, "core-runtime-prequalification",
            "CORE_PREQUALIFICATION", Arch7bExecutionKind.ChildInvoke, "node",
            new string('c', 64), ["src/fast-seal-cli.mjs", "prequalify-bracket-runtime",
                "--config", configPath], coreNodeRoot, "core-prequalification-v1",
            Arch7bV2Contracts.ChildResultAdapterVersion,
            "lmax_portal_core_runtime_prequalification_v1", 30, 1_048_576,
            1_048_576, "qualification-child-process", false, false, false,
            [], [], null, Path.Combine(authorityRoot, "command-authority.json"),
            new string('d', 64), new string('e', 64));
        var prepared = new Arch7bPreparedCorePrequalificationConfig(
            ValidConfig(), validBytes, Sha(validBytes), new string('f', 64));

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bOneShotLiveExecutionRuntimeV2.ValidateCorePrequalificationPreSpawnAsync(
                command, template, runRoot, prepared, CancellationToken.None));

        Assert.Equal(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
            error.BlockerCode);
        Assert.NotEqual(Arch7bBlockers.ChildOutputInvalid, error.BlockerCode);
        var evidencePath = Path.Combine(authorityRoot,
            "core-prequalification-pre-spawn-config-failure.json");
        Assert.True(File.Exists(evidencePath));
        using var evidence = JsonDocument.Parse(File.ReadAllBytes(evidencePath));
        Assert.Equal("arch7b_core_prequalification_pre_spawn_config_failure_v1",
            evidence.RootElement.GetProperty("contract_version").GetString());
        Assert.False(evidence.RootElement.GetProperty("child_process_started").GetBoolean());
        Assert.False(evidence.RootElement.GetProperty("child_receipt_present").GetBoolean());
        Assert.Empty(Directory.EnumerateFiles(runRoot, "*receipt*",
            SearchOption.AllDirectories));
    }

    [Fact]
    public void Serializer_never_emits_browser_channel_or_snake_case()
    {
        var json = Encoding.UTF8.GetString(ValidBytes());

        Assert.DoesNotContain("browserChannel", json, StringComparison.Ordinal);
        Assert.DoesNotContain("repository_root", json, StringComparison.Ordinal);
        Assert.DoesNotContain("browser_executable_path", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_property_in_canonical_position_is_rejected()
    {
        AssertBlocker(ReplaceName("expectedTree", "otherTree"),
            Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch);
    }

    private Arch7bCorePrequalificationConfigV1 Parse(byte[] bytes) =>
        Arch7bCorePrequalificationConfigParser.ParseAndValidate(bytes, Context());

    private Arch7bCorePrequalificationConfigV1 ValidConfig() => new(
        repositoryRoot,
        Path.Combine(runRoot, "core-prequalification-output"),
        commit,
        tree,
        chromePath,
        chromeSha);

    private Arch7bCorePrequalificationConfigValidationContext Context() => new(
        repositoryRoot, coreNodeRoot, runRoot, commit, tree, chromePath, chromeSha);

    private byte[] ValidBytes() => Serialize(ValidConfig());

    private static byte[] Serialize(Arch7bCorePrequalificationConfigV1 value) =>
        Arch7bCorePrequalificationConfigSerializer.Serialize(value);

    private byte[] ReplaceName(string expected, string replacement)
    {
        var json = Encoding.UTF8.GetString(ValidBytes());
        return Encoding.UTF8.GetBytes(json.Replace("\"" + expected + "\"",
            "\"" + replacement + "\"", StringComparison.Ordinal));
    }

    private byte[] InsertBeforeClose(string value)
    {
        var json = Encoding.UTF8.GetString(ValidBytes());
        return Encoding.UTF8.GetBytes(json.Insert(json.Length - 1, value));
    }

    private Arch7bQualificationException AssertBlocker(byte[] bytes, string blocker)
    {
        var error = Assert.Throws<Arch7bQualificationException>(() => Parse(bytes));
        Assert.Equal(blocker, error.BlockerCode);
        return error;
    }

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static Arch7bOneShotStageContract Rehash(Arch7bOneShotStageContract stage)
    {
        var canonical = string.Join('\n', stage.StageId, stage.ExecutionKind,
            string.Join('|', stage.Predecessors), string.Join('|', stage.RequiredFactTypes),
            string.Join('|', stage.ProducedFactTypes), stage.SloId ?? string.Empty,
            stage.ValidatorId);
        return stage with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical) };
    }

    private static string SupervisorExecutable()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        var repository = current?.FullName ??
                         throw new DirectoryNotFoundException("repository root");
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(repository, "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor", "bin", "Release",
            "net10.0", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor" + extension);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}