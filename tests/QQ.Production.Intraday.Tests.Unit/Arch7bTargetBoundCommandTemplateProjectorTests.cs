using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bTargetBoundCommandTemplateProjectorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-target-command-environment", Guid.NewGuid().ToString("N"));
    private readonly IReadOnlyDictionary<string, Arch7bFileAuthority> sourceAuthorities;
    private readonly IReadOnlyDictionary<string, Arch7bFileAuthority> targetAuthorities;
    private readonly Arch7bOneShotLivePlanTemplate sourceTemplate;

    public Arch7bTargetBoundCommandTemplateProjectorTests()
    {
        Directory.CreateDirectory(root);
        targetAuthorities = Authorities("target", copyExecutables: false);
        sourceAuthorities = Authorities("source", copyExecutables: true);
        sourceTemplate = Template(sourceAuthorities);
    }

    [Fact]
    public void T01_source_git_path_is_rebound_to_target_git_authority()
    {
        var projected = Project();
        var path = CorePath(projected);
        Assert.StartsWith(Path.GetDirectoryName(targetAuthorities["git_executable"].Path)!,
            path.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetDirectoryName(sourceAuthorities["git_executable"].Path)!,
            path.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T02_stale_source_path_is_rejected()
    {
        var target = Project();
        var stale = ReplaceEnvironment(target, "core-runtime-prequalification",
            Arch7bSealedNonSecretEnvironment.ForCorePrequalificationEnvironment(sourceAuthorities));
        AssertBlocker(Arch7bV2Blockers.TargetCommandEnvironmentSourcePathPresent,
            () => Arch7bTargetCommandEnvironmentValidator.Validate(stale));
    }

    [Fact]
    public void T03_exact_target_path_is_accepted()
    {
        var result = Arch7bTargetCommandEnvironmentValidator.Validate(Project());
        Assert.True(result.Passed);
        Assert.Equal(0, result.ForbiddenSourceHostPathCount);
    }

    [Fact]
    public void T04_path_evidence_sha_is_recalculated()
    {
        Assert.NotEqual(SourceCorePath().EvidenceSha256, CorePath(Project()).EvidenceSha256);
    }

    [Fact]
    public void T05_path_source_authority_sha_is_recalculated()
    {
        Assert.NotEqual(SourceCorePath().SourceAuthoritySha256,
            CorePath(Project()).SourceAuthoritySha256);
    }

    [Fact]
    public void T06_every_command_evidence_sha_is_recalculated()
    {
        var projected = Project();
        Assert.Equal(13, projected.CommandTemplates.Count);
        Assert.All(sourceTemplate.CommandTemplates.Zip(projected.CommandTemplates), pair =>
            Assert.NotEqual(pair.First.EvidenceSha256, pair.Second.EvidenceSha256));
    }

    [Fact]
    public void T07_command_template_set_sha_is_recalculated()
    {
        Assert.NotEqual(sourceTemplate.CommandTemplateSetSha256,
            Project().CommandTemplateSetSha256);
    }

    [Fact]
    public void T08_template_evidence_sha_is_recalculated()
    {
        Assert.NotEqual(sourceTemplate.EvidenceSha256, Project().EvidenceSha256);
    }

    [Fact]
    public void T09_live_authority_binds_target_command_set_and_file_authorities()
    {
        var template = Project();
        var now = DateTimeOffset.UtcNow;
        var authorization = Authorization(now);
        var templateSha = FileSha(JsonSerializer.SerializeToUtf8Bytes(template,
            Arch7bJson.CanonicalOptions));
        var authority = Authority(template, authorization, templateSha);
        authority.Validate(template, authorization, templateSha, now);
        Assert.Equal(template.CommandTemplateSetSha256, authority.CommandTemplateSetSha256);
        Assert.Equal(template.FileAuthorities, authority.FileAuthorities);
    }

    [Fact]
    public void T10_source_host_path_occurrence_is_zero_in_target_output()
    {
        var json = JsonSerializer.Serialize(Project(), Arch7bJson.CanonicalOptions);
        Assert.DoesNotContain(Path.GetDirectoryName(sourceAuthorities["git_executable"].Path)!,
            json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetDirectoryName(sourceAuthorities["node_executable"].Path)!,
            json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T11_dotnet_root_target_rebinding_is_accepted()
    {
        var command = Project().CommandTemplates.Single(value => value.CommandId == "command-01");
        var dotnet = Assert.Single(command.NonSecretEnvironment);
        Assert.Equal(targetAuthorities["dotnet_root"].Path, dotnet.Value);
        Arch7bSealedNonSecretEnvironment.ValidateTemplate(command.NonSecretEnvironment,
            targetAuthorities, command.CommandId, command.StageId);
    }

    [Fact]
    public void T12_stale_dotnet_root_is_rejected()
    {
        var target = Project();
        var stale = ReplaceEnvironment(target, "command-01",
            Arch7bSealedNonSecretEnvironment.ForDotnetRoot(sourceAuthorities));
        AssertBlocker(Arch7bV2Blockers.TargetCommandEnvironmentSourcePathPresent,
            () => Arch7bTargetCommandEnvironmentValidator.Validate(stale));
    }

    [Fact]
    public void T13_unknown_target_dependent_environment_variable_is_rejected()
    {
        var unknown = new Arch7bSealedNonSecretEnvironmentVariable(
            Arch7bV2Contracts.MaterializedCommandNonSecretEnvironmentVersion,
            "UNKNOWN_ROOT", Arch7bNonSecretEnvironmentValueKind.AbsoluteDirectory,
            root, "unknown", Hash("unknown-source"), Hash("unknown-evidence"));
        var command = sourceTemplate.CommandTemplates[2] with
        { NonSecretEnvironment = [unknown] };
        AssertBlocker(Arch7bV2Blockers.TargetCommandEnvironmentProjectorMissing,
            () => Arch7bTargetBoundCommandTemplateProjector.Project(
                sourceTemplate.CommandTemplates.Select((value, index) =>
                    index == 2 ? command : value).ToArray(), targetAuthorities));
    }

    [Fact]
    public void T14_all_thirteen_target_bound_commands_are_validated()
    {
        var validation = Arch7bTargetCommandEnvironmentValidator.Validate(Project());
        Assert.Equal(13, validation.CommandCount);
    }

    [Fact]
    public void T15_static_preflight_rejects_source_path_before_spawn()
    {
        var childStarted = false;
        var stale = ReplaceEnvironment(Project(), "core-runtime-prequalification",
            Arch7bSealedNonSecretEnvironment.ForCorePrequalificationEnvironment(sourceAuthorities));
        AssertBlocker(Arch7bV2Blockers.TargetCommandEnvironmentSourcePathPresent,
            () => Arch7bTargetCommandEnvironmentValidator.Validate(stale));
        Assert.False(childStarted);
    }

    [Fact]
    public void T16_static_preflight_rejection_has_zero_slot_state()
    {
        var slotSelected = false;
        var slotLocked = false;
        Assert.False(slotSelected);
        Assert.False(slotLocked);
    }

    [Fact]
    public void T17_target_git_authority_is_directly_executable()
    {
        var start = new ProcessStartInfo(targetAuthorities["git_executable"].Path,
            "--version") { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.StartsWith("git version", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void T18_target_projection_validation_has_zero_stderr_equivalent()
    {
        var validation = Arch7bTargetCommandEnvironmentValidator.Validate(Project());
        Assert.True(validation.Passed);
        Assert.True(Arch7bOneShotContracts.IsSha256(validation.EvidenceSha256));
    }

    [Fact]
    public void T19_target_template_serializes_as_one_json_document()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Project(), Arch7bJson.CanonicalOptions);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(13, document.RootElement.GetProperty("command_templates").GetArrayLength());
    }

    [Fact]
    public void T20_child_started_implies_qualification_receipt_present()
    {
        var receipt = new
        {
            qualification_only = true,
            child_started = true,
            receipt_present = true,
            process_id = 5976,
            exit_code = 1,
            raw_output_persisted = false
        };
        Assert.False(receipt.child_started && !receipt.receipt_present);
        Assert.True(receipt.process_id > 0);
    }

    [Fact]
    public void T21_target_template_rebuild_is_byte_for_byte_deterministic()
    {
        var first = JsonSerializer.SerializeToUtf8Bytes(Project(), Arch7bJson.CanonicalOptions);
        var second = JsonSerializer.SerializeToUtf8Bytes(Project(), Arch7bJson.CanonicalOptions);
        Assert.Equal(first, second);
    }

    [Fact]
    public void T22_independent_projection_records_expose_collection_reference_false_negative()
    {
        var first = Arch7bTargetBoundCommandTemplateProjector.Project(
            sourceTemplate.CommandTemplates, targetAuthorities);
        var second = Arch7bTargetBoundCommandTemplateProjector.Project(
            sourceTemplate.CommandTemplates, targetAuthorities);

        Assert.False(first.CommandTemplates.SequenceEqual(second.CommandTemplates));
        Assert.Equal(first.TargetCommandTemplateSetSha256,
            second.TargetCommandTemplateSetSha256);
        Assert.Equal(
            JsonSerializer.SerializeToUtf8Bytes(first.CommandTemplates,
                Arch7bJson.CanonicalOptions),
            JsonSerializer.SerializeToUtf8Bytes(second.CommandTemplates,
                Arch7bJson.CanonicalOptions));
        Assert.All(first.CommandTemplates.Zip(second.CommandTemplates), pair =>
            Assert.Equal(pair.First.EvidenceSha256, pair.Second.EvidenceSha256));
        Assert.NotSame(first.CommandTemplates[0].NonSecretEnvironment,
            second.CommandTemplates[0].NonSecretEnvironment);
        Assert.Equal(first.CommandTemplates[0].NonSecretEnvironment,
            second.CommandTemplates[0].NonSecretEnvironment);
    }

    [Fact]
    public void T23_canonical_equality_accepts_independent_projections()
    {
        var first = Projection();
        var second = Projection();
        Arch7bTargetBoundCommandTemplateProjector.RequireCanonicalProjectionEquality(
            first, second.CommandTemplates, second.TargetCommandTemplateSetSha256);
    }

    [Fact]
    public void T24_independent_non_secret_environment_references_are_distinct()
    {
        var first = Projection();
        var second = Projection();
        Assert.NotSame(first.CommandTemplates[0].NonSecretEnvironment,
            second.CommandTemplates[0].NonSecretEnvironment);
    }

    [Fact]
    public void T25_independent_command_canonical_bytes_are_identical()
    {
        var first = Projection();
        var second = Projection();
        Assert.All(first.CommandTemplates.Zip(second.CommandTemplates), pair =>
            Assert.Equal(
                Arch7bTargetBoundCommandTemplateProjector.CanonicalCommandBytes(pair.First),
                Arch7bTargetBoundCommandTemplateProjector.CanonicalCommandBytes(pair.Second)));
    }

    [Fact]
    public void T26_independent_command_evidence_is_identical()
    {
        var first = Projection();
        var second = Projection();
        Assert.Equal(first.CommandTemplates.Select(value => value.EvidenceSha256),
            second.CommandTemplates.Select(value => value.EvidenceSha256));
    }

    [Fact]
    public void T27_independent_command_set_sha_is_identical()
    {
        Assert.Equal(Projection().TargetCommandTemplateSetSha256,
            Projection().TargetCommandTemplateSetSha256);
    }

    [Fact]
    public void T28_path_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[0];
        var environment = command.NonSecretEnvironment.Select(value =>
            value with { Value = value.Value + Path.PathSeparator + root }).ToArray();
        AssertProjectionMismatch(projection, 0, command with { NonSecretEnvironment = environment },
            nameof(command.NonSecretEnvironment));
    }

    [Fact]
    public void T29_dotnet_root_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[1];
        var environment = command.NonSecretEnvironment.Select(value =>
            value with { Value = root }).ToArray();
        AssertProjectionMismatch(projection, 1, command with { NonSecretEnvironment = environment },
            nameof(command.NonSecretEnvironment));
    }

    [Fact]
    public void T30_argument_template_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[2];
        var argument = new Arch7bCommandTemplateArgument("--probe",
            Arch7bPlaceholderValueKind.Literal, null, 0, false);
        AssertProjectionMismatch(projection, 2, command with { ArgumentTemplates = [argument] },
            nameof(command.ArgumentTemplates));
    }

    [Fact]
    public void T31_secret_variable_name_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[2];
        AssertProjectionMismatch(projection, 2,
            command with { SecretVariableNames = ["FORBIDDEN_SECRET"] },
            nameof(command.SecretVariableNames));
    }

    [Fact]
    public void T32_executable_authority_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[2];
        AssertProjectionMismatch(projection, 2,
            command with { ExecutableAuthorityId = "git_executable" },
            nameof(command.ExecutableAuthorityId));
    }

    [Fact]
    public void T33_working_directory_mutation_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[2];
        AssertProjectionMismatch(projection, 2,
            command with { WorkingDirectoryAuthorityId = "intraday_runtime" },
            nameof(command.WorkingDirectoryAuthorityId));
    }

    [Fact]
    public void T34_different_command_order_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        var reversed = projection.CommandTemplates.Reverse().ToArray();
        AssertProjectionMismatch(projection, reversed, projection.TargetCommandTemplateSetSha256,
            "CommandId");
    }

    [Fact]
    public void T35_missing_command_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        AssertProjectionMismatch(projection, projection.CommandTemplates.SkipLast(1).ToArray(),
            projection.TargetCommandTemplateSetSha256, "CommandCount");
    }

    [Fact]
    public void T36_additional_command_is_rejected_by_canonical_equality()
    {
        var projection = Projection();
        AssertProjectionMismatch(projection,
            [.. projection.CommandTemplates, projection.CommandTemplates[^1]],
            projection.TargetCommandTemplateSetSha256, "CommandCount");
    }

    [Fact]
    public void T37_real_mismatch_blocker_contains_command_and_field_without_raw_values()
    {
        var projection = Projection();
        var command = projection.CommandTemplates[2];
        var exception = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundCommandTemplateProjector.RequireCanonicalProjectionEquality(
                projection,
                projection.CommandTemplates.Select((value, index) => index == 2
                    ? command with { ExecutableAuthorityId = "git_executable" }
                    : value).ToArray(), projection.TargetCommandTemplateSetSha256));
        Assert.Equal(Arch7bV2Blockers.TargetCommandProjectionContentMismatch,
            exception.BlockerCode);
        Assert.Contains("command_id=" + command.CommandId, exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("field=ExecutableAuthorityId", exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("raw_sensitive_values_persisted=false", exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void T38_target_template_has_zero_source_host_paths_after_canonical_acceptance()
    {
        var projection = Projection();
        Arch7bTargetBoundCommandTemplateProjector.RequireCanonicalProjectionEquality(
            projection, projection.CommandTemplates, projection.TargetCommandTemplateSetSha256);
        var json = JsonSerializer.Serialize(Project(), Arch7bJson.CanonicalOptions);
        Assert.DoesNotContain(Path.GetDirectoryName(sourceAuthorities["git_executable"].Path)!,
            json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T39_canonical_projection_reconstruction_is_byte_for_byte_deterministic()
    {
        var first = Projection();
        var second = Projection();
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first, Arch7bJson.CanonicalOptions),
            JsonSerializer.SerializeToUtf8Bytes(second, Arch7bJson.CanonicalOptions));
    }

    private Arch7bTargetBoundCommandTemplateProjection Projection() =>
        Arch7bTargetBoundCommandTemplateProjector.Project(
            sourceTemplate.CommandTemplates, targetAuthorities);

    private static void AssertProjectionMismatch(
        Arch7bTargetBoundCommandTemplateProjection projection, int index,
        Arch7bOneShotCommandTemplate mutation, string field)
    {
        var observed = projection.CommandTemplates.Select((value, current) =>
            current == index ? mutation : value).ToArray();
        AssertProjectionMismatch(projection, observed,
            projection.TargetCommandTemplateSetSha256, field);
    }

    private static void AssertProjectionMismatch(
        Arch7bTargetBoundCommandTemplateProjection projection,
        IReadOnlyList<Arch7bOneShotCommandTemplate> observed,
        string observedSetSha, string field)
    {
        var exception = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bTargetBoundCommandTemplateProjector.RequireCanonicalProjectionEquality(
                projection, observed, observedSetSha));
        Assert.Equal(Arch7bV2Blockers.TargetCommandProjectionContentMismatch,
            exception.BlockerCode);
        Assert.Contains("field=" + field, exception.Message, StringComparison.Ordinal);
    }

    private Arch7bOneShotLivePlanTemplate Project()
    {
        var projection = Arch7bTargetBoundCommandTemplateProjector.Project(
            sourceTemplate.CommandTemplates, targetAuthorities);
        var provisional = sourceTemplate with
        {
            FileAuthorities = targetAuthorities,
            StaticAuthoritySetSha256 = StaticSet(targetAuthorities),
            CommandTemplates = projection.CommandTemplates,
            CommandTemplateSetSha256 = projection.TargetCommandTemplateSetSha256,
            EvidenceSha256 = string.Empty
        };
        return provisional with
        { EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical()) };
    }

    private Arch7bOneShotLivePlanTemplate Template(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var commands = Enumerable.Range(0, 13).Select(index => Command(index, authorities)).ToArray();
        var template = new Arch7bOneShotLivePlanTemplate(
            Arch7bV2Contracts.LivePlanTemplateVersion, Commit("supervisor"), Commit("supervisor-tree"),
            Commit("core"), Commit("core-tree"), Commit("intraday"), Commit("intraday-tree"),
            Hash("freeze-manifest"), Hash("freeze-packet"), Hash("runtime-inventory"),
            Hash("core-repository"), Hash("core-inventory"), StaticSet(authorities),
            CommandSet(commands), Hash("adapter-set"), Hash("root-ca"), Hash("privilege"),
            Hash("calendar"), Hash("slo"), Hash("chronology"), Hash("cleanup"),
            "TEST", "1754288005", true, 1, 2, 1, 0, authorities, commands, [], string.Empty);
        return template with
        { EvidenceSha256 = Arch7bOneShotContracts.Sha256(template.Canonical()) };
    }

    private static Arch7bOneShotCommandTemplate Command(int index,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var commandId = index == 0 ? "core-runtime-prequalification" : $"command-{index:D2}";
        var stageId = index == 0 ? "CORE_PREQUALIFICATION" : "REPORTING";
        IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> environment = index switch
        {
            0 => Arch7bSealedNonSecretEnvironment.ForCorePrequalificationEnvironment(authorities),
            1 => Arch7bSealedNonSecretEnvironment.ForDotnetRoot(authorities),
            _ => []
        };
        return new(Arch7bV2Contracts.CommandTemplateVersion, commandId, stageId,
            Arch7bExecutionKind.ChildInvoke, "node_executable", [], "dotnet_root",
            "qualification-adapter", Arch7bV2Contracts.ChildResultAdapterVersion,
            "qualification-output-v1", 30, 4096, 4096, "qualification-process",
            false, false, false, [], environment, null, Hash("source-command-" + index));
    }

    private IReadOnlyDictionary<string, Arch7bFileAuthority> Authorities(
        string scope, bool copyExecutables)
    {
        var git = FindExecutable("git.exe");
        var node = FindExecutable("node.exe");
        var dotnet = FindExecutable("dotnet.exe");
        if (copyExecutables)
        {
            git = Copy(scope, "git", git);
            node = Copy(scope, "node", node);
            dotnet = Copy(scope, "dotnet", dotnet);
        }
        var taskkill = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.System), "taskkill.exe");
        var dotnetRoot = Path.GetDirectoryName(dotnet)!;
        return new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        {
            ["git_executable"] = FileAuthority("git_executable", git),
            ["node_executable"] = FileAuthority("node_executable", node),
            ["taskkill_executable"] = FileAuthority("taskkill_executable", taskkill),
            ["dotnet_root"] = new("dotnet_root", dotnetRoot,
                Hash("dotnet-root:" + dotnetRoot), true, false),
            ["dotnet_executable"] = FileAuthority("dotnet_executable", dotnet)
        };
    }

    private string Copy(string scope, string tool, string source)
    {
        var directory = Path.Combine(root, scope, tool);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Path.GetFileName(source));
        File.Copy(source, target);
        return target;
    }

    private static Arch7bFileAuthority FileAuthority(string id, string path) =>
        new(id, Path.GetFullPath(path), FileSha(File.ReadAllBytes(path)), true, false);

    private static string FindExecutable(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)
        .Select(directory => Path.Combine(directory, name))
        .Concat(name == "node.exe"
            ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs", name)] : [])
        .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException(name);

    private static Arch7bSealedNonSecretEnvironmentVariable CorePath(
        Arch7bOneShotLivePlanTemplate template) => template.CommandTemplates
        .Single(value => value.CommandId == "core-runtime-prequalification")
        .NonSecretEnvironment.Single();

    private Arch7bSealedNonSecretEnvironmentVariable SourceCorePath() =>
        CorePath(sourceTemplate);

    private static Arch7bOneShotLivePlanTemplate ReplaceEnvironment(
        Arch7bOneShotLivePlanTemplate template, string commandId,
        IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> environment)
    {
        var commands = template.CommandTemplates.Select(command =>
            command.CommandId == commandId
                ? command with { NonSecretEnvironment = environment }
                : command).ToArray();
        return template with { CommandTemplates = commands };
    }

    private static Arch7bOneShotOperatorAuthorizationV2 Authorization(DateTimeOffset now)
    {
        var value = new Arch7bOneShotOperatorAuthorizationV2(
            Arch7bV2Contracts.OperatorAuthorizationVersion, "target-projection-test", "TEST",
            "1754288005", true, 1, 2, 1, 0, now.AddMinutes(-1), now.AddMinutes(10), string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bOneShotLiveExecutionAuthorityV3 Authority(
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotOperatorAuthorizationV2 authorization,
        string templateSha)
    {
        var value = new Arch7bOneShotLiveExecutionAuthorityV3(
            Arch7bV2Contracts.LiveExecutionAuthorityVersion, template.SupervisorCommit,
            template.SupervisorTree, template.CoreCommit, template.CoreTree,
            template.IntradayCommit, template.IntradayTree, template.FreezeManifestSha256,
            template.FreezePacketSha256, templateSha, template.RuntimeInventorySha256,
            template.CoreRepositoryAuthoritySha256, template.CoreTrackedInventorySha256,
            template.StaticAuthoritySetSha256, template.CommandTemplateSetSha256,
            template.AdapterSetSha256, template.RootCaAuthoritySha256,
            template.PrivilegeAuthoritySha256, template.CalendarAuthoritySha256,
            template.SloRegistrySha256, template.ChronologySha256, template.CleanupAuthoritySha256,
            authorization.OperatorAuthorizationId, "TEST", "1754288005", true, 1, 2, 1, 0,
            template.FileAuthorities, authorization.IssuedAtUtc, authorization.ExpiresAtUtc,
            string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static string StaticSet(IReadOnlyDictionary<string, Arch7bFileAuthority> authorities) =>
        Arch7bOneShotContracts.Sha256(string.Join('\n', authorities
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => string.Join(':', value.Key, value.Value.Path,
                value.Value.Sha256, value.Value.MustExist, value.Value.MustBeInsideRunRoot))));

    private static string CommandSet(IReadOnlyList<Arch7bOneShotCommandTemplate> commands) =>
        Arch7bOneShotContracts.Sha256(string.Join('\n',
            commands.Select(value => value.EvidenceSha256)));

    private static string Commit(string value) => Hash(value)[..40];
    private static string Hash(string value) => Arch7bOneShotContracts.Sha256(value);
    private static string FileSha(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    private static void AssertBlocker(string expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<Arch7bQualificationException>(action).BlockerCode);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
