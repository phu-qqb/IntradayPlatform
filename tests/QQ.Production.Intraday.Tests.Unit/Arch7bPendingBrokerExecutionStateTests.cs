using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPendingBrokerExecutionStateTests
{
    [Fact]
    public async Task Pms_import_is_started_once_and_completed_once()
    {
        var state = new Arch7bPendingBrokerExecutionState();
        var expected = Result();

        state.StartPmsImport(new string('a', 64), _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.True(state.HasPmsImport);
        Assert.Equal(new string('a', 64), state.PmsImportCommandSha256);
        Assert.Same(expected, await state.CompletePmsImportAsync());
        Assert.False(state.HasPmsImport);
        await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            state.CompletePmsImportAsync());
    }

    [Fact]
    public void Duplicate_pms_import_start_is_rejected()
    {
        var state = new Arch7bPendingBrokerExecutionState();
        var pending = new TaskCompletionSource<Arch7bCoreBrokerCommandResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.StartPmsImport(new string('a', 64), _ => pending.Task,
            CancellationToken.None);

        Assert.Throws<Arch7bQualificationException>(() => state.StartPmsImport(
            new string('b', 64), _ => Task.FromResult(Result()),
            CancellationToken.None));
        state.CancelPending();
    }

    [Fact]
    public async Task Terminal_cancel_reaches_pending_broker_execution()
    {
        var state = new Arch7bPendingBrokerExecutionState();
        state.StartPmsImport(new string('a', 64), async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result();
        }, CancellationToken.None);

        state.CancelPending();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            state.CompletePmsImportAsync());
        Assert.False(state.HasPmsImport);
    }

    private static Arch7bCoreBrokerCommandResult Result() => new(
        new(Arch7bCoreRdsSecretBrokerContracts.Response, "pms-economic-replay",
            "PMS_IMPORT", 1, "POST_BRACKET", "SUCCESS", new string('b', 64),
            "arch6f_economic_replay_v2", new string('c', 64), 1,
            new string('d', 64), "{}"),
        new(Arch7bV2Contracts.ChildResultAdapterVersion, "pms-economic-replay-v1",
            Arch7bV2Contracts.ChildResultAdapterVersion, "arch6f_economic_replay_v2",
            "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED", [], [], 0,
            new string('e', 64)));
}
