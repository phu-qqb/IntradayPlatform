using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLiveExecutionRuntimeTests
{
    [Fact]
    public async Task Process_level_success_executes_all_40_stages_and_cleans_resources()
    {
        var executable = SupervisorExecutable();
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-live-tests", Guid.NewGuid().ToString("N"));
        var fixture = Arch7bSyntheticLiveExecutionFactory.Create(executable, root, "unit-success");
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());

        var result = await runtime.RunOneShotAsync(fixture.Plan, fixture.Authority,
            fixture.Authority.EvidenceSha256, fixture.Plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);

        Assert.True(result.Passed, JsonSerializer.Serialize(result));
        Assert.Equal(40, result.Stages.Count);
        Assert.Equal(new Arch7bOneShotBudgetSnapshot(1, 2, 1, 0), result.Budget);
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.ResidualMarkerCount);
        if (Directory.Exists(root)) Directory.Delete(root, true);
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
