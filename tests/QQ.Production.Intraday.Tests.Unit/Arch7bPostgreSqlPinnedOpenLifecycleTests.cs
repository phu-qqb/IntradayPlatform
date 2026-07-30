using System.Data;
using System.Text.Json;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlPinnedOpenLifecycleTests
{
    [Fact]
    public async Task Successful_open_is_closed_and_disposed_exactly_once()
    {
        using var root = new TempRoot();
        var runtime = FakeRuntime.Success();
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        var open = await supervisor.WaitForOpenAsync();
        var lifecycle = await supervisor.CompleteAsync();

        Assert.Equal(1, open.PhysicalOpenCount);
        Assert.Equal(0, open.PhysicalReconnectCount);
        Assert.Equal("COMPLETED", lifecycle.CleanupResult);
        Assert.Equal(1, runtime.CloseCount);
        Assert.Equal(1, runtime.ConnectionDisposeCount);
        Assert.Equal(1, runtime.DataSourceDisposeCount);
        Assert.Equal(ConnectionState.Closed, runtime.ConnectionState);
    }

    [Fact]
    public async Task Cleanup_failure_never_masks_primary_timeout()
    {
        using var root = new TempRoot();
        var primary = new TimeoutException("fixture timeout");
        FakeRuntime? runtime = null;
        runtime = new FakeRuntime(
            _ =>
            {
                runtime!.State = ConnectionState.Connecting;
                return Task.FromException<
                    Arch7bPostgreSqlPinnedSessionEvidence>(primary);
            },
            (_, _, _, _) => throw new InvalidOperationException(
                "secondary cleanup"));
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        var observed = await Assert.ThrowsAsync<TimeoutException>(
            supervisor.WaitForOpenAsync);
        var lifecycle = await supervisor.CompleteAsync(observed);
        var terminal = Assert.Throws<TimeoutException>(
            supervisor.RethrowPrimary);

        Assert.Same(primary, terminal);
        Assert.Equal(typeof(InvalidOperationException).FullName,
            lifecycle.CleanupFailureType);
        Assert.True(lifecycle.CleanupFailureSuppressedByPrimary);
        Assert.Equal(
            Arch7bPostgreSqlPinnedOpenLifecycleContract.OpenAsyncTimeout,
            lifecycle.PrimaryFailureCode);
        Assert.True(File.Exists(System.IO.Path.Combine(root.Path,
            "pinned-open-lifecycle-failure.json")));
    }

    [Fact]
    public async Task Cleanup_failure_without_primary_is_terminal_and_evidenced()
    {
        using var root = new TempRoot();
        FakeRuntime? runtime = null;
        runtime = new FakeRuntime(
            _ =>
            {
                runtime!.State = ConnectionState.Open;
                return Task.FromResult(Evidence());
            },
            (_, _, _, _) => throw new InvalidOperationException(
                "cleanup only"));
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        _ = await supervisor.WaitForOpenAsync();
        var terminal = await Assert.ThrowsAsync<InvalidDataException>(
            () => supervisor.CompleteAsync());

        Assert.Equal(
            Arch7bPostgreSqlPinnedOpenLifecycleContract
                .CleanupFailedWithoutPrimary,
            terminal.Message);
        Assert.True(File.Exists(System.IO.Path.Combine(root.Path,
            "pinned-open-lifecycle-failure.json")));
    }

    [Fact]
    public async Task Hard_deadline_cancels_and_defers_stuck_connecting_cleanup()
    {
        using var root = new TempRoot();
        var pending = new TaskCompletionSource<
            Arch7bPostgreSqlPinnedSessionEvidence>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = FakeRuntime.StuckConnecting(pending.Task);
        var supervisor = Supervisor(
            runtime, root.Path, TimeSpan.FromMilliseconds(20));

        _ = supervisor.StartOpen();
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            supervisor.WaitForOpenAsync);
        var lifecycle = await supervisor.CompleteAsync(failure);

        Assert.Equal(
            Arch7bPostgreSqlPinnedOpenLifecycleContract
                .HardDeadlineExceeded,
            lifecycle.PrimaryFailureCode);
        Assert.Equal(
            Arch7bPostgreSqlPinnedOpenLifecycleContract
                .ConnectingCleanupDeferred,
            lifecycle.CleanupResult);
        Assert.True(lifecycle.ProcessExitRequiredForHandleCleanup);
        Assert.Equal(0, runtime.CloseCount);
        Assert.Equal(0, runtime.ConnectionDisposeCount);
        Assert.Equal(0, runtime.DataSourceDisposeCount);
    }

    [Fact]
    public async Task Cancellation_and_npgsql_failures_are_classified()
    {
        await AssertClassification(
            new OperationCanceledException("fixture"),
            "OPENASYNC_CANCELLATION");
        await AssertClassification(
            new NpgsqlException("fixture"),
            Arch7bPostgreSqlPinnedOpenLifecycleContract
                .PrimaryOpenFailure);
    }

    [Fact]
    public async Task Connecting_can_settle_closed_during_cleanup_grace()
    {
        using var root = new TempRoot();
        var runtime = FakeRuntime.CancelSettles(ConnectionState.Closed);
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            supervisor.WaitForOpenAsync);
        var lifecycle = await supervisor.CompleteAsync(failure);

        Assert.False(lifecycle.ProcessExitRequiredForHandleCleanup);
        Assert.Equal(0, runtime.CloseCount);
        Assert.Equal(1, runtime.ConnectionDisposeCount);
        Assert.Equal(1, runtime.DataSourceDisposeCount);
    }

    [Fact]
    public async Task Connecting_can_settle_open_then_close_during_cleanup_grace()
    {
        using var root = new TempRoot();
        var runtime = FakeRuntime.CancelSettles(ConnectionState.Open);
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        var failure = await Assert.ThrowsAsync<TimeoutException>(
            supervisor.WaitForOpenAsync);
        var lifecycle = await supervisor.CompleteAsync(failure);

        Assert.Equal("COMPLETED", lifecycle.CleanupResult);
        Assert.Equal(1, runtime.CloseCount);
        Assert.Equal(1, runtime.ConnectionDisposeCount);
        Assert.Equal(1, runtime.DataSourceDisposeCount);
    }

    [Fact]
    public async Task Core_failure_during_open_is_primary_without_aggregate()
    {
        using var root = new TempRoot();
        var pending = new TaskCompletionSource<
            Arch7bPostgreSqlPinnedSessionEvidence>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = FakeRuntime.StuckConnecting(pending.Task);
        var supervisor = Supervisor(runtime, root.Path);
        var core = new InvalidDataException("CORE_PACKAGE_INVALID");

        _ = supervisor.StartOpen();
        var terminal = await Assert.ThrowsAsync<InvalidDataException>(() =>
            supervisor.WaitForOpenAndPeerAsync(
                Task.FromException<int>(core)));
        _ = await supervisor.CompleteAsync(terminal);

        Assert.Same(core, terminal);
        Assert.IsNotType<AggregateException>(terminal);
    }

    [Fact]
    public async Task Open_failure_during_core_validation_is_primary()
    {
        using var root = new TempRoot();
        var primary = new TimeoutException("open failed");
        var runtime = FakeRuntime.Failure(primary);
        var supervisor = Supervisor(runtime, root.Path);
        var core = Task.Run(async () =>
        {
            await Task.Delay(30);
            return 7;
        });

        _ = supervisor.StartOpen();
        var terminal = await Assert.ThrowsAsync<TimeoutException>(() =>
            supervisor.WaitForOpenAndPeerAsync(core));
        var lifecycle = await supervisor.CompleteAsync(terminal);

        Assert.Same(primary, terminal);
        Assert.Empty(lifecycle.SecondaryFailureTypes);
    }

    [Fact]
    public async Task Double_failure_preserves_first_monotonic_failure()
    {
        using var root = new TempRoot();
        var primary = new TimeoutException("open first");
        var runtime = FakeRuntime.Failure(primary, TimeSpan.FromMilliseconds(5));
        var supervisor = Supervisor(runtime, root.Path);
        var peer = Task.Run(async () =>
        {
            await Task.Delay(25);
            throw new InvalidDataException("core second");
#pragma warning disable CS0162
            return 0;
#pragma warning restore CS0162
        });

        _ = supervisor.StartOpen();
        var terminal = await Assert.ThrowsAsync<TimeoutException>(() =>
            supervisor.WaitForOpenAndPeerAsync(peer));
        var lifecycle = await supervisor.CompleteAsync(terminal);

        Assert.Same(primary, terminal);
        Assert.Contains(typeof(InvalidDataException).FullName!,
            lifecycle.SecondaryFailureTypes);
        Assert.IsNotType<AggregateException>(terminal);
    }

    [Fact]
    public async Task Lifecycle_evidence_is_sanitized_and_completion_idempotent()
    {
        using var root = new TempRoot();
        var runtime = FakeRuntime.Success();
        var supervisor = Supervisor(runtime, root.Path);

        _ = supervisor.StartOpen();
        _ = await supervisor.WaitForOpenAsync();
        var first = await supervisor.CompleteAsync();
        var second = await supervisor.CompleteAsync();
        var json = File.ReadAllText(System.IO.Path.Combine(
            root.Path, "pinned-open-lifecycle.json"));

        Assert.Equal(first, second);
        Assert.DoesNotContain("password", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection_string", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretString", json,
            StringComparison.Ordinal);
        Assert.Equal(64, first.EvidenceSha256.Length);
        _ = JsonDocument.Parse(json);
    }

    [Fact]
    public void Static_contract_places_open_in_try_and_removes_await_using()
    {
        var root = RepoRoot();
        var session = File.ReadAllText(System.IO.Path.Combine(root, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bPostgreSqlPinnedSession.cs"));
        var open = session.IndexOf("await connection.OpenAsync(",
            StringComparison.Ordinal);
        var precedingTry = session.LastIndexOf("try", open,
            StringComparison.Ordinal);
        var precedingMethod = session.LastIndexOf(
            "Task<Arch7bPostgreSqlPinnedSessionEvidence> OpenAsync",
            open, StringComparison.Ordinal);

        Assert.True(precedingTry > precedingMethod);
        foreach (var path in new[]
                 {
                     System.IO.Path.Combine(root, "tools",
                         "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
                         "Program.cs"),
                     System.IO.Path.Combine(root, "tools",
                         "QQ.Production.Intraday.Tools.Arch7bGlobalFlatPositionSnapshot",
                         "Program.cs")
                 })
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("await using var runtime", source,
                StringComparison.Ordinal);
            Assert.Contains(
                "Arch7bPostgreSqlPinnedOpenSupervisor", source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Position_import_preconditions_precede_runtime_and_open()
    {
        var source = File.ReadAllText(System.IO.Path.Combine(
            RepoRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
            "Program.cs"));

        Assert.True(source.IndexOf(
                "Arch7bPositionImportArguments.Parse(args)",
                StringComparison.Ordinal) <
            source.IndexOf("arguments.BuildRuntime()",
                StringComparison.Ordinal));
        Assert.Contains("ExpectedTargetFingerprint ==", source,
            StringComparison.Ordinal);
        Assert.True(source.IndexOf("prevalidatedRepository",
            StringComparison.Ordinal) <
            source.IndexOf("arguments.BuildRuntime()",
                StringComparison.Ordinal));
        Assert.True(source.IndexOf("arguments.BuildRuntime()",
            StringComparison.Ordinal) <
            source.IndexOf("supervisor.StartOpen()",
                StringComparison.Ordinal));
    }

    private static async Task AssertClassification(
        Exception failure,
        string expectedCode)
    {
        using var root = new TempRoot();
        var runtime = FakeRuntime.Failure(failure);
        var supervisor = Supervisor(runtime, root.Path);
        _ = supervisor.StartOpen();

        var observed = await Assert.ThrowsAnyAsync<Exception>(
            supervisor.WaitForOpenAsync);
        var lifecycle = await supervisor.CompleteAsync(observed);

        Assert.Same(failure, observed);
        Assert.Equal(expectedCode, lifecycle.PrimaryFailureCode);
    }

    private static Arch7bPostgreSqlPinnedOpenSupervisor Supervisor(
        FakeRuntime runtime,
        string root,
        TimeSpan? hardDeadline = null) =>
        new(runtime, "fixture", root,
            hardDeadline ?? TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(25));

    private sealed class FakeRuntime : IArch7bPostgreSqlPinnedOpenRuntime
    {
        private readonly Func<CancellationToken,
            Task<Arch7bPostgreSqlPinnedSessionEvidence>> open;
        private readonly Func<Task, CancellationTokenSource, Exception?,
            TimeSpan, Task<Arch7bPostgreSqlPinnedCleanupResult>>? complete;

        public FakeRuntime(
            Func<CancellationToken,
                Task<Arch7bPostgreSqlPinnedSessionEvidence>> open,
            Func<Task, CancellationTokenSource, Exception?, TimeSpan,
                Task<Arch7bPostgreSqlPinnedCleanupResult>>? complete = null)
        {
            this.open = open;
            this.complete = complete;
        }

        public ConnectionState ConnectionState => State;
        public ConnectionState State { get; set; } = ConnectionState.Closed;
        public int CloseCount { get; private set; }
        public int ConnectionDisposeCount { get; private set; }
        public int DataSourceDisposeCount { get; private set; }

        public Task<Arch7bPostgreSqlPinnedSessionEvidence> OpenAsync(
            CancellationToken cancellationToken = default) =>
            open(cancellationToken);

        public async Task<Arch7bPostgreSqlPinnedCleanupResult> CompleteAsync(
            Task openTask,
            CancellationTokenSource openCancellation,
            Exception? primaryFailure,
            TimeSpan cancellationGrace)
        {
            if (complete is not null)
                return await complete(
                    openTask, openCancellation, primaryFailure,
                    cancellationGrace);
            var before = ConnectionState;
            if (ConnectionState == ConnectionState.Connecting)
            {
                openCancellation.Cancel();
                try { await openTask.WaitAsync(cancellationGrace); }
                catch { }
                if (ConnectionState == ConnectionState.Connecting)
                    return new(
                        Arch7bPostgreSqlPinnedOpenLifecycleContract
                            .ConnectingCleanupDeferred,
                        before,
                        ConnectionState,
                        true,
                        null,
                        primaryFailure is not null,
                        true);
            }
            if (ConnectionState == ConnectionState.Open)
            {
                CloseCount++;
                State = ConnectionState.Closed;
            }
            ConnectionDisposeCount++;
            DataSourceDisposeCount++;
            return new(
                "COMPLETED",
                before,
                ConnectionState,
                true,
                null,
                false,
                false);
        }

        public static FakeRuntime Success()
        {
            FakeRuntime? runtime = null;
            runtime = new FakeRuntime(_ =>
            {
                runtime!.State = ConnectionState.Open;
                return Task.FromResult(Evidence());
            });
            return runtime;
        }

        public static FakeRuntime Failure(
            Exception failure,
            TimeSpan? delay = null)
        {
            FakeRuntime? runtime = null;
            runtime = new FakeRuntime(async _ =>
            {
                runtime!.State = ConnectionState.Connecting;
                if (delay is not null) await Task.Delay(delay.Value);
                runtime!.State = ConnectionState.Closed;
                throw failure;
            });
            return runtime;
        }

        public static FakeRuntime StuckConnecting(
            Task<Arch7bPostgreSqlPinnedSessionEvidence> pending)
        {
            FakeRuntime? runtime = null;
            runtime = new FakeRuntime(_ =>
            {
                runtime!.State = ConnectionState.Connecting;
                return pending;
            });
            return runtime;
        }

        public static FakeRuntime CancelSettles(
            ConnectionState settledState)
        {
            FakeRuntime? runtime = null;
            runtime = new FakeRuntime(async cancellationToken =>
            {
                runtime!.State = ConnectionState.Connecting;
                try
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    runtime!.State = settledState;
                }
                throw new OperationCanceledException(cancellationToken);
            });
            return runtime;
        }
    }

    private static Arch7bPostgreSqlPinnedSessionEvidence Evidence() =>
        new(
            "fixture",
            Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary
                .ContractVersion,
            DateTimeOffset.UtcNow,
            1,
            4242,
            "redacted",
            Arch7bBracketedGlobalFlatContract.TargetDatabase,
            "redacted",
            "18.4",
            "UTC",
            true,
            new string('a', 64),
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            false,
            "Open",
            new string('b', 64));

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"arch7b-open-lifecycle-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(System.IO.Path.Combine(
                   directory.FullName,
                   "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        return directory?.FullName ??
               throw new DirectoryNotFoundException();
    }
}
