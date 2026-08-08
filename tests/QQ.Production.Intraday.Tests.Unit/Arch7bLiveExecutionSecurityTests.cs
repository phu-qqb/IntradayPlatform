using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLiveExecutionSecurityTests
{
    [Fact]
    public void Execution_gap_records_that_merged_master_cannot_execute_children()
    {
        var value = Arch7bOneShotSupervisorExecutionGap.Create("3ed8c928eb33063a900ac0d8fa4262c1fe349546");

        Assert.True(value.QualificationOnlyRequired);
        Assert.False(value.RunOneShotModePresent);
        Assert.False(value.RealProcessRunnerPresent);
        Assert.False(value.RealChildEvidenceConsumed);
        Assert.Equal(Arch7bOneShotContracts.ExecutionGapVerdict, value.Verdict);
        Assert.True(Arch7bOneShotContracts.IsSha256(value.EvidenceSha256));
    }

    [Fact]
    public void Duplicate_arguments_are_rejected_without_last_value_wins()
    {
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Program.Parse(["--mode", "validate-one-shot-plan", "--mode", "run-one-shot"]));

        Assert.Equal(Arch7bBlockers.DuplicateArgument, error.BlockerCode);
    }

    [Theory]
    [InlineData("expired", Arch7bBlockers.LiveAuthorityExpired)]
    [InlineData("operator", Arch7bBlockers.OperatorAuthorizationMismatch)]
    [InlineData("environment", Arch7bBlockers.TargetEnvironmentNotTest)]
    [InlineData("no-order", Arch7bBlockers.NoOrderRequired)]
    [InlineData("commit", Arch7bBlockers.LiveAuthorityCommitMismatch)]
    [InlineData("freeze", Arch7bBlockers.FreezeAuthorityMismatch)]
    [InlineData("commands", Arch7bBlockers.CommandAuthorityMismatch)]
    [InlineData("budget", Arch7bBlockers.LiveCommandAuthorityIncomplete)]
    public void Live_authority_mismatches_fail_closed(string mutation, string blocker)
    {
        var fixture = Fixture("authority-" + mutation);
        var authority = mutation switch
        {
            "expired" => fixture.Authority with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "operator" => fixture.Authority with { OperatorAuthorizationId = "different" },
            "environment" => fixture.Authority with { TargetEnvironment = "PRODUCTION" },
            "no-order" => fixture.Authority with { NoOrder = false },
            "commit" => fixture.Authority with { IntradayCommit = new string('a', 40) },
            "freeze" => fixture.Authority with { FreezeManifestSha256 = new string('a', 64) },
            "commands" => fixture.Authority with { CommandAuthoritySetSha256 = new string('a', 64) },
            "budget" => fixture.Authority with { MaximumSlots = 2 },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        authority = authority with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(authority.Canonical()) };

        var error = Assert.Throws<Arch7bQualificationException>(() => authority.Validate(fixture.Plan,
            authority.EvidenceSha256, fixture.Plan.OperatorAuthorizationId, DateTimeOffset.UtcNow));

        Assert.Equal(blocker, error.BlockerCode);
    }

    [Fact]
    public void Plan_evidence_is_content_addressed()
    {
        var fixture = Fixture("plan-evidence");
        var plan = fixture.Plan with { EvidenceSha256 = new string('0', 64) };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotAuthorityLoader.ValidatePlan(plan));

        Assert.Equal(Arch7bBlockers.CommandAuthorityMismatch, error.BlockerCode);
    }

    [Fact]
    public void Secret_arguments_shells_relative_paths_and_ambient_path_are_rejected()
    {
        var fixture = Fixture("command-validation");
        var command = fixture.Plan.Commands[0];
        Assert.Equal(Arch7bBlockers.SecretInArgument, Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotAuthorityLoader.ValidateCommand(command with
            {
                ArgumentList = [.. command.ArgumentList, "password=forbidden"]
            }, fixture.Plan.RunRoot)).BlockerCode);
        Assert.Equal(Arch7bBlockers.AmbientPathForbidden, Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotAuthorityLoader.ValidateCommand(command with
            {
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "cmd.exe")
            }, fixture.Plan.RunRoot)).BlockerCode);
        Assert.Equal(Arch7bBlockers.AbsolutePathRequired, Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOneShotAuthorityLoader.ValidateCommand(command with
            {
                ExecutablePath = "fake-child"
            }, fixture.Plan.RunRoot)).BlockerCode);
    }

    [Fact]
    public async Task Existing_run_root_is_rejected_before_any_child_starts()
    {
        var fixture = Fixture("existing-root");
        Directory.CreateDirectory(fixture.Plan.RunRoot);
        await File.WriteAllTextAsync(Path.Combine(fixture.Plan.RunRoot, "existing.txt"), "do-not-reuse");
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() => runtime.RunOneShotAsync(
            fixture.Plan, fixture.Authority, fixture.Authority.EvidenceSha256,
            fixture.Plan.OperatorAuthorizationId, DateTimeOffset.UtcNow));

        Assert.Equal(Arch7bBlockers.RunRootNotEmpty, error.BlockerCode);
        Directory.Delete(fixture.Plan.RunRoot, true);
    }

    [Theory]
    [InlineData("invalid-json", Arch7bBlockers.ChildOutputInvalid)]
    [InlineData("bad-sha", Arch7bBlockers.ChildOutputShaMismatch)]
    [InlineData("missing-evidence", Arch7bBlockers.ChildEvidenceMissing)]
    [InlineData("secret-sentinel", Arch7bBlockers.ChildOutputSecretDetected)]
    public async Task Real_child_output_failures_are_catalogued_and_stop_the_dag(string behavior, string blocker)
    {
        var result = await ExecuteAsync("STATIC_AUTHORITY_VALIDATION", behavior);

        Assert.False(result.Passed);
        Assert.Equal(blocker, result.PrimaryFailure?.FirstBlockerCode);
        Assert.Equal("STATIC_AUTHORITY_VALIDATION", result.PrimaryFailure?.FailureStage);
        Assert.Empty(result.Stages);
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.Budget.Retries);
    }

    [Fact]
    public async Task Executable_sha_mismatch_is_rejected_before_process_start()
    {
        var fixture = Fixture("executable-sha");
        var plan = MutateCommand(fixture.Plan, 0, value => value with
        {
            ExecutableSha256 = new string('0', 64)
        });
        var authority = RebindAuthority(fixture.Authority, plan);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());

        var result = await runtime.RunOneShotAsync(plan, authority, authority.EvidenceSha256,
            plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);

        Assert.Equal(Arch7bBlockers.ExecutableShaMismatch, result.PrimaryFailure?.FirstBlockerCode);
        Assert.Empty(result.Stages);
        Assert.True(result.Cleanup.Complete);
    }

    [Fact]
    public async Task Secret_read_after_bracket_is_rejected_before_next_process_start()
    {
        var fixture = Fixture("secret-after-bracket");
        var index = fixture.Plan.Commands.ToList().FindIndex(value => value.StageId == "BRACKET_P1");
        var plan = MutateCommand(fixture.Plan, index, value => value with { ReadsSecret = true });
        var authority = RebindAuthority(fixture.Authority, plan);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());

        var result = await runtime.RunOneShotAsync(plan, authority, authority.EvidenceSha256,
            plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);

        Assert.Equal(Arch7bBlockers.SecretReadAfterBracket, result.PrimaryFailure?.FirstBlockerCode);
        Assert.DoesNotContain(result.Stages, value => value.StageId == "BRACKET_P1");
        Assert.True(result.Cleanup.Complete);
    }

    [Fact]
    public async Task Marker_created_by_real_child_is_removed_by_terminal_cleanup()
    {
        var result = await ExecuteAsync("STATIC_AUTHORITY_VALIDATION", "marker");

        Assert.True(result.Passed, JsonSerializer.Serialize(result));
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.ResidualMarkerCount);
    }

    [Fact]
    public async Task Timed_out_child_is_killed_and_cleanup_completes()
    {
        var fixture = Fixture("timeout", "STATIC_AUTHORITY_VALIDATION", "timeout");
        var plan = MutateCommand(fixture.Plan, 0, value => value with { TimeoutSeconds = 1 });
        var authority = RebindAuthority(fixture.Authority, plan);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());

        var result = await runtime.RunOneShotAsync(plan, authority, authority.EvidenceSha256,
            plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);

        Assert.False(result.Passed);
        Assert.Equal(Arch7bBlockers.ChildProcessTimeout, result.PrimaryFailure?.FirstBlockerCode);
        Assert.Equal("STATIC_AUTHORITY_VALIDATION", result.PrimaryFailure?.FailureStage);
        Assert.Empty(result.Stages);
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.Budget.Retries);
        if (Directory.Exists(plan.RunRoot)) Directory.Delete(plan.RunRoot, true);
    }

    [Fact]
    public async Task Run_one_shot_refuses_qualification_only_mode()
    {
        var exitCode = await Program.Main(["--mode", "run-one-shot", "--qualification-only", "true"]);

        Assert.Equal(2, exitCode);
    }

    private static async Task<Arch7bOneShotLiveExecutionEvidence> ExecuteAsync(string stage, string behavior)
    {
        var fixture = Fixture(behavior, stage, behavior);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());
        var result = await runtime.RunOneShotAsync(fixture.Plan, fixture.Authority,
            fixture.Authority.EvidenceSha256, fixture.Plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);
        if (Directory.Exists(fixture.Plan.RunRoot)) Directory.Delete(fixture.Plan.RunRoot, true);
        return result;
    }

    private static (Arch7bOneShotLivePlan Plan, Arch7bOneShotLiveExecutionAuthority Authority) Fixture(
        string suffix, string? failureStage = null, string failureBehavior = "blocker")
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-live-tests", suffix + "-" + Guid.NewGuid().ToString("N"));
        return Arch7bSyntheticLiveExecutionFactory.Create(SupervisorExecutable(), root,
            "unit-" + suffix + "-" + Guid.NewGuid().ToString("N"), failureStage, failureBehavior);
    }

    private static Arch7bOneShotLivePlan MutateCommand(Arch7bOneShotLivePlan plan, int index,
        Func<Arch7bOneShotCommandAuthority, Arch7bOneShotCommandAuthority> mutation)
    {
        var commands = plan.Commands.ToArray();
        commands[index] = mutation(commands[index]);
        commands[index] = commands[index] with { EvidenceSha256 = Arch7bOneShotContracts.Sha256("mutated:" + commands[index]) };
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n', commands.Select(value => value.EvidenceSha256)));
        var changed = plan with { Commands = commands, CommandAuthoritySetSha256 = commandSet };
        var canonical = string.Join('\n', changed.ContractVersion, changed.CoreCommit, changed.CoreTree,
            changed.IntradayCommit, changed.IntradayTree, changed.FreezeManifestSha256, changed.FreezePacketSha256,
            changed.CommandAuthoritySetSha256, changed.OperatorAuthorizationId, changed.RunId, changed.RunRoot,
            changed.NoOrder, changed.Synthetic);
        return changed with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical) };
    }

    private static Arch7bOneShotLiveExecutionAuthority RebindAuthority(
        Arch7bOneShotLiveExecutionAuthority authority, Arch7bOneShotLivePlan plan)
    {
        var changed = authority with { CommandAuthoritySetSha256 = plan.CommandAuthoritySetSha256 };
        return changed with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(changed.Canonical()) };
    }

    private static string SupervisorExecutable()
    {
        var root = FindRepositoryRoot();
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "bin", "Release", "net10.0", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor" + extension);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }
}
