using System.Text;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bProcessRuntimeV2Tests
{
    [Theory]
    [InlineData("malformed", Arch7bBlockers.ChildOutputInvalid)]
    [InlineData("missing-artifact", Arch7bBlockers.ChildEvidenceMissing)]
    [InlineData("wrong-sha", Arch7bBlockers.ChildOutputShaMismatch)]
    [InlineData("wrong-cardinality", Arch7bV2Blockers.ChildNativeArtifactCardinality)]
    [InlineData("unknown-status", Arch7bV2Blockers.ChildNativeStatusUnknown)]
    [InlineData("overflow", Arch7bV2Blockers.ChildOutputLimitExceeded)]
    [InlineData("secret-value", Arch7bV2Blockers.ChildOutputSecretValueDetected)]
    [InlineData("crash", Arch7bBlockers.ChildOutputInvalid)]
    public async Task Native_adapter_and_stream_failures_preserve_the_first_exact_blocker(
        string behavior, string expectedBlocker)
    {
        var root = Root("negative-" + behavior);
        var fixture = Arch7bV2QualificationFactory.Create(SupervisorExecutable(), root,
            "CORE_PREQUALIFICATION", behavior);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);

        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, root,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease());

        Assert.False(result.Passed);
        Assert.Equal(expectedBlocker, result.PrimaryFailure?.FirstBlockerCode);
        Assert.Equal("CORE_PREQUALIFICATION", result.PrimaryFailure?.FailureStage);
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.ResidualProcessCount);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public async Task Streaming_reader_blocks_exact_secret_and_never_records_raw_output()
    {
        const string secret = "qualification-only-secret-value";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("prefix-" + secret + "-suffix"));

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bBoundedStreamReader.ReadAsync(stream, 1024, [secret], [], CancellationToken.None));

        Assert.Equal(Arch7bV2Blockers.ChildOutputSecretValueDetected, error.BlockerCode);
    }

    [Fact]
    public async Task Streaming_reader_enforces_byte_limit_during_read()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 257)));

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bBoundedStreamReader.ReadAsync(stream, 256, [], [], CancellationToken.None));

        Assert.Equal(Arch7bV2Blockers.ChildOutputLimitExceeded, error.BlockerCode);
    }

    [Fact]
    public void Scoped_secret_lease_is_command_bound_two_read_bounded_and_forbidden_after_bracket()
    {
        var values = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["read-1"] = new Dictionary<string, string> { ["ARCH7B_TEST_SECRET"] = "one" },
            ["read-2"] = new Dictionary<string, string> { ["ARCH7B_TEST_SECRET"] = "two" },
            ["read-3"] = new Dictionary<string, string> { ["ARCH7B_TEST_SECRET"] = "three" }
        };
        using var lease = new Arch7bScopedSecretLease(values);
        var first = lease.Acquire("read-1", ["ARCH7B_TEST_SECRET"], false);
        lease.Release(first);
        var second = lease.Acquire("read-2", ["ARCH7B_TEST_SECRET"], false);
        lease.Release(second);

        Assert.Equal(2, lease.ReadCount);
        Assert.Equal(Arch7bBlockers.RdsReadLimitExceeded,
            Assert.Throws<Arch7bQualificationException>(() => lease.Acquire("read-3",
                ["ARCH7B_TEST_SECRET"], false)).BlockerCode);
        Assert.Equal(Arch7bBlockers.SecretReadAfterBracket,
            Assert.Throws<Arch7bQualificationException>(() => lease.Acquire("read-1",
                ["ARCH7B_TEST_SECRET"], true)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.SecretCommandScopeMismatch,
            Assert.Throws<Arch7bQualificationException>(() => new Arch7bScopedSecretLease(values)
                .Acquire("read-1", ["WRONG_VARIABLE"], false)).BlockerCode);
    }

    [Fact]
    public void Adapter_registry_is_explicit_complete_and_rejects_unknown_adapter()
    {
        var registry = new Arch7bRealCommandAdapterRegistry();

        var required = Arch7bFinalStageExecutionCatalog.All
            .Where(value => value.HasCommandTemplate)
            .Select(value => value.AdapterId!).Distinct(StringComparer.Ordinal).ToArray();
        Assert.All(required, adapterId => Assert.Same(
            registry.Require(adapterId), registry.Require(adapterId)));
        Assert.Equal(registry.Adapters.Count,
            registry.Adapters.Select(value => value.AdapterId).Distinct().Count());
        Assert.All(registry.Adapters, value =>
        {
            Assert.Equal(Arch7bV2Contracts.ChildResultAdapterVersion, value.ContractVersion);
            Assert.False(string.IsNullOrWhiteSpace(value.ExpectedNativeOutputContract));
        });
        Assert.Equal(Arch7bV2Blockers.ChildAdapterMissing,
            Assert.Throws<Arch7bQualificationException>(() => registry.Require("generic-permissive"))
                .BlockerCode);
    }

    [Fact]
    public void Core_owned_secret_contract_never_returns_a_secret_to_the_supervisor()
    {
        var lease = new Arch7bCoreOwnedSecretLease();
        var empty = lease.Acquire("core-handoff", [], false);

        Assert.Empty(empty.Values);
        Assert.Equal(0, empty.SecretValueCount);
        Assert.Equal(0, lease.ReadCount);
        Assert.Equal(Arch7bV2Blockers.SecretCommandScopeMismatch,
            Assert.Throws<Arch7bQualificationException>(() => lease.Acquire("core-handoff",
                ["PGPASSWORD"], false)).BlockerCode);
    }

    [Fact]
    public async Task Long_lived_completion_signal_is_atomic_across_repeated_processes()
    {
        for (var index = 0; index < 20; index++)
        {
            var timeProvider = new Arch7bTestTimeProvider(DateTimeOffset.UtcNow);
            var result = await Arch7bV2ProcessQualifier.RunSingleAsync(SupervisorExecutable(),
                $"atomic-completion-{index:D2}", timeProvider: timeProvider,
                clockAuthorityProducer: new Arch7bTestClockAuthorityProducer(timeProvider));

            Assert.True(result.Passed);
            Assert.Equal(Arch7bOneShotContracts.ExpectedFinalBlocker, result.FinalBlocker);
            Assert.Equal(40, result.Stages.Count);
            Assert.Equal(2, result.LongLivedProcesses.Count);
            Assert.All(result.LongLivedProcesses,
                process => Assert.Equal(Arch7bLongLivedProcessState.Cleaned, process.State));
            Assert.Equal(0, result.ResidualProcessCount);
            Assert.Equal(0, result.ResidualMarkerCount);
        }
    }

    private static string Root(string suffix) => Path.Combine(Path.GetTempPath(),
        "qq-arch7b-v2-process-tests", suffix + "-" + Guid.NewGuid().ToString("N"));

    private static string SupervisorExecutable()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        var root = current?.FullName ?? throw new DirectoryNotFoundException("repository root");
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "bin", "Release", "net10.0", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor" + extension);
    }
}
