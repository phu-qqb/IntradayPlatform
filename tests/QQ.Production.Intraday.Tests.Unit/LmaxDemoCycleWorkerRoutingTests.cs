using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QQ.Production.Intraday.Worker;
using System.Reflection;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoCycleWorkerRoutingTests
{
    [Fact]
    public async Task EnabledCycleRoutesToItsManifestBeforeTheOrdinaryLoopWhenImmediateStartupIsDisabled()
    {
        var missingManifest = Path.Combine(Path.GetTempPath(), $"lmax-demo-cycle-{Guid.NewGuid():N}.json");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LmaxDemoCycle:Enabled"] = "true",
                ["LmaxDemoCycle:ManifestPath"] = missingManifest,
                ["Worker:ProcessImmediatelyOnStartup"] = "false",
                ["Worker:PollInterval"] = "00:00:00"
            })
            .Build();
        var worker = new Worker(null!, configuration, NullLogger<Worker>.Instance, null!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteAsyncForTest(worker, CancellationToken.None));

        Assert.Equal("LMAX_DEMO_CYCLE_MANIFEST_NOT_FOUND", exception.Message);
    }

    private static Task ExecuteAsyncForTest(Worker worker, CancellationToken cancellationToken)
        => (Task)(typeof(Worker)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(worker, [cancellationToken])
            ?? throw new InvalidOperationException("WORKER_EXECUTE_ASYNC_NOT_INVOKED"));
}
