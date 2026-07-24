using System.Diagnostics;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bBoundedMarketDataCleanupTests
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task Blocked_unsubscribe_is_cancelled_and_valid_bbo_remains_allowed()
    {
        var blocked = new BlockingWriteStream();
        var stopwatch = Stopwatch.StartNew();

        var cleanup = await RunCleanupAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            token => BlockedWriteAsync(blocked, token),
            _ => Task.FromResult(true));

        stopwatch.Stop();
        Assert.True(cleanup.UnsubscribeAttempted);
        Assert.False(cleanup.UnsubscribeSent);
        Assert.True(cleanup.LogoutAttempted);
        Assert.True(cleanup.LogoutSent);
        Assert.True(blocked.CancellationObserved);
        Assert.Equal(0, blocked.ActiveWrites);
        Assert.True(cleanup.ForceCloseAttempted);
        Assert.True(cleanup.ForceCloseSucceeded);
        Assert.Contains(
            cleanup.Diagnostics,
            value => value.StartsWith(
                "ARCH7B_MARKET_DATA_UNSUBSCRIBE_TIMEOUT:",
                StringComparison.Ordinal));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));

        var result = Observation(cleanup);
        var decision = Evaluate(result);
        Assert.Equal("Ok", result.Status);
        Assert.True(result.CompleteTopOfBook);
        Assert.True(result.InboundSequenceIntegrityProven);
        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
    }

    [Fact]
    public async Task Blocked_logout_is_cancelled_and_valid_bbo_remains_allowed()
    {
        var blocked = new BlockingWriteStream();
        var stopwatch = Stopwatch.StartNew();

        var cleanup = await RunCleanupAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            _ => Task.FromResult(true),
            token => BlockedWriteAsync(blocked, token));

        stopwatch.Stop();
        Assert.True(cleanup.UnsubscribeSent);
        Assert.True(cleanup.LogoutAttempted);
        Assert.False(cleanup.LogoutSent);
        Assert.True(blocked.CancellationObserved);
        Assert.Equal(0, blocked.ActiveWrites);
        Assert.True(cleanup.ForceCloseAttempted);
        Assert.True(cleanup.ForceCloseSucceeded);
        Assert.Contains(
            cleanup.Diagnostics,
            value => value.StartsWith(
                "ARCH7B_MARKET_DATA_LOGOUT_TIMEOUT:",
                StringComparison.Ordinal));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.True(Evaluate(Observation(cleanup)).Allowed);
    }

    [Fact]
    public async Task Blocked_unsubscribe_and_logout_share_one_total_budget()
    {
        var unsubscribe = new BlockingWriteStream();
        var logout = new BlockingWriteStream();
        var stopwatch = Stopwatch.StartNew();

        var cleanup = await RunCleanupAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            token => BlockedWriteAsync(unsubscribe, token),
            token => BlockedWriteAsync(logout, token));

        stopwatch.Stop();
        Assert.True(cleanup.UnsubscribeAttempted);
        Assert.True(cleanup.LogoutAttempted);
        Assert.False(cleanup.UnsubscribeSent);
        Assert.False(cleanup.LogoutSent);
        Assert.Equal(1, unsubscribe.WriteCount);
        Assert.Equal(1, logout.WriteCount);
        Assert.True(unsubscribe.CancellationObserved);
        Assert.True(logout.CancellationObserved);
        Assert.Equal(0, unsubscribe.ActiveWrites);
        Assert.Equal(0, logout.ActiveWrites);
        Assert.True(
            stopwatch.Elapsed <= TestBudget + TimeSpan.FromMilliseconds(600),
            $"Cleanup elapsed {stopwatch.Elapsed} for budget {TestBudget}.");
    }

    [Fact]
    public async Task Nearly_expired_deadline_caps_the_shared_cleanup_budget()
    {
        var unsubscribe = new BlockingWriteStream();
        var logout = new BlockingWriteStream();
        var lifecycleDeadlineUtc = DateTimeOffset.UtcNow.AddMilliseconds(80);
        var stopwatch = Stopwatch.StartNew();

        var cleanup = await RunCleanupAsync(
            lifecycleDeadlineUtc,
            TimeSpan.FromSeconds(2),
            token => BlockedWriteAsync(unsubscribe, token),
            token => BlockedWriteAsync(logout, token));

        stopwatch.Stop();
        Assert.True(cleanup.DeadlineUtc <= lifecycleDeadlineUtc);
        Assert.True(cleanup.UnsubscribeAttempted);
        Assert.True(cleanup.LogoutAttempted);
        Assert.False(cleanup.UnsubscribeSent);
        Assert.False(cleanup.LogoutSent);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(700));
        Assert.True(Evaluate(Observation(cleanup)).Allowed);
    }

    [Fact]
    public async Task Expired_deadline_starts_no_write_and_force_closes_immediately()
    {
        var unsubscribeCalls = 0;
        var logoutCalls = 0;
        var forceCloseCalls = 0;
        var stopwatch = Stopwatch.StartNew();

        var cleanup = await LmaxFixMarketDataCleanup.RunAsync(
            DateTimeOffset.UtcNow.AddMilliseconds(-1),
            TestBudget,
            "REQ-1",
            _ =>
            {
                unsubscribeCalls++;
                return Task.FromResult(true);
            },
            _ =>
            {
                logoutCalls++;
                return Task.FromResult(true);
            },
            () => forceCloseCalls++,
            () => { },
            () => { },
            default);

        stopwatch.Stop();
        Assert.Equal(0, unsubscribeCalls);
        Assert.Equal(0, logoutCalls);
        Assert.False(cleanup.UnsubscribeAttempted);
        Assert.False(cleanup.LogoutAttempted);
        Assert.True(cleanup.ForceCloseAttempted);
        Assert.True(cleanup.ForceCloseSucceeded);
        Assert.Equal(1, forceCloseCalls);
        Assert.Contains(
            "ARCH7B_MARKET_DATA_CLEANUP_DEADLINE_EXHAUSTED",
            cleanup.Diagnostics);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public async Task Successful_cleanup_preserves_mdreqid_and_records_actual_disposals()
    {
        const string mdReqId = "REQ-1";
        var requestOptions = RequestOptions();
        string? unsubscribeMessage = null;
        var logoutCalls = 0;
        var forceCloseCalls = 0;
        var streamDisposeCalls = 0;
        var socketDisposeCalls = 0;

        var cleanup = await LmaxFixMarketDataCleanup.RunAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            mdReqId,
            _ =>
            {
                unsubscribeMessage = LmaxFixMarketDataCodec.BuildMarketDataRequest(
                    "SENDER",
                    "LMXBDM",
                    3,
                    mdReqId,
                    requestOptions,
                    unsubscribe: true);
                return Task.FromResult(true);
            },
            _ =>
            {
                logoutCalls++;
                return Task.FromResult(true);
            },
            () => forceCloseCalls++,
            () => streamDisposeCalls++,
            () => socketDisposeCalls++,
            default);

        Assert.True(cleanup.UnsubscribeSent);
        Assert.Equal(mdReqId, cleanup.UnsubscribeMdReqId);
        Assert.Equal("2", LmaxFixMarketDataCodec.GetTag(unsubscribeMessage!, "263"));
        Assert.Equal(mdReqId, LmaxFixMarketDataCodec.GetTag(unsubscribeMessage!, "262"));
        Assert.True(cleanup.LogoutSent);
        Assert.Equal(1, logoutCalls);
        Assert.Equal(1, forceCloseCalls);
        Assert.Equal(1, streamDisposeCalls);
        Assert.Equal(1, socketDisposeCalls);
        Assert.True(cleanup.StreamDisposeSucceeded);
        Assert.True(cleanup.SocketDisposeSucceeded);
        Assert.Empty(cleanup.Diagnostics);
        Assert.True(Evaluate(Observation(cleanup)).Allowed);
    }

    [Fact]
    public async Task Successful_cleanup_never_makes_invalid_bbo_acceptable()
    {
        var cleanup = await RunCleanupAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var decision = Evaluate(Observation(cleanup, bid: 1.25010m, ask: 1.25000m));
        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_FLATTEN_BBO_INVALID", decision.Blockers);
    }

    [Fact]
    public async Task Returned_cleanup_snapshot_is_final_and_diagnostics_are_immutable()
    {
        var diagnostics = new List<string> { "one" };
        var snapshot = new LmaxFixMarketDataCleanupSnapshot(
            unsubscribeAttempted: true,
            diagnostics: diagnostics);
        diagnostics.Add("two");

        Assert.Equal(["one"], snapshot.Diagnostics);
        var list = Assert.IsAssignableFrom<IList<string>>(snapshot.Diagnostics);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add("three"));

        var cleanup = await RunCleanupAsync(
            DateTimeOffset.UtcNow.AddSeconds(2),
            TestBudget,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));
        Assert.True(cleanup.UnsubscribeSent);
        Assert.True(cleanup.LogoutSent);
        Assert.True(cleanup.StreamDisposeSucceeded);
        Assert.True(cleanup.SocketDisposeSucceeded);
    }

    [Fact]
    public void Cleanup_source_has_no_none_token_or_fire_and_forget()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "LmaxFixMarketDataCleanup.cs"));

        var rawClientSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.cs"));
        var cleanupPathStart = rawClientSource.IndexOf(
            "async Task<bool> TryUnsubscribeAsync",
            StringComparison.Ordinal);
        var cleanupPathEnd = rawClientSource.IndexOf(
            "private static async Task<Stream> CreateTlsStreamAsync",
            cleanupPathStart,
            StringComparison.Ordinal);
        var boundedCleanupPath = rawClientSource[cleanupPathStart..cleanupPathEnd];

        Assert.DoesNotContain("CancellationToken.None", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CancellationToken.None", boundedCleanupPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ContinueWith", source, StringComparison.Ordinal);
        Assert.DoesNotContain("async void", source, StringComparison.Ordinal);
        Assert.Contains("await action(operationToken)", source, StringComparison.Ordinal);
        Assert.Equal(
            1000,
            Arch7bKnownOrderQualificationPolicy.MaximumMarketDataCleanupMilliseconds);
    }

    private static Task<LmaxFixMarketDataCleanupSnapshot> RunCleanupAsync(
        DateTimeOffset deadlineUtc,
        TimeSpan budget,
        Func<CancellationToken, Task<bool>> unsubscribe,
        Func<CancellationToken, Task<bool>> logout)
        => LmaxFixMarketDataCleanup.RunAsync(
            deadlineUtc,
            budget,
            "REQ-1",
            unsubscribe,
            logout,
            () => { },
            () => { },
            () => { },
            default);

    private static async Task<bool> BlockedWriteAsync(
        BlockingWriteStream stream,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(new byte[] { 1 }, cancellationToken);
        return true;
    }

    private static LmaxFixArch7bMarketObservationDecision Evaluate(
        LmaxFixMarketDataSmokeResult result)
    {
        var now = DateTimeOffset.UtcNow;
        return LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            MarketDataOptions(),
            result,
            now.AddSeconds(-3),
            now,
            new string('a', 64));
    }

    private static LmaxFixMarketDataSmokeResult Observation(
        LmaxFixMarketDataCleanupSnapshot cleanup,
        decimal bid = 1.24990m,
        decimal ask = 1.25000m)
    {
        var now = DateTimeOffset.UtcNow;
        return LmaxFixMarketDataSmokeResult.Create(
            "Ok",
            "test",
            now.AddSeconds(-2),
            true,
            true,
            true,
            true,
            true,
            true,
            false,
            cleanup.LogoutSent,
            null,
            null,
            "X",
            [],
            [],
            [],
            bestBid: bid,
            bestAsk: ask,
            mid: (bid + ask) / 2m,
            messageCount: 2) with
        {
            CompletedAtUtc = now,
            ObservationCompletedAtUtc = now.AddSeconds(-1),
            InboundSequenceIntegrityProven = true,
            SnapshotSha256 = new string('b', 64),
            RequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MdReqId = "REQ-1",
            CompleteTopOfBook = true,
            Cleanup = cleanup
        };
    }

    private static LmaxFixMarketDataRequestOptions RequestOptions()
        => new(
            Arch7bKnownOrderQualificationPolicy.Symbol,
            Arch7bKnownOrderQualificationPolicy.SecurityId,
            "GBP/USD",
            1,
            LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds,
            10,
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol,
            Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
            false);

    private static LmaxConnectivityLabOptions MarketDataOptions()
        => new()
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = false,
            AllowLiveTrading = false,
            DryRun = false,
            FixSenderCompId = "SENDER",
            InstrumentSymbol = Arch7bKnownOrderQualificationPolicy.Symbol,
            LmaxInstrumentId = Arch7bKnownOrderQualificationPolicy.SecurityId,
            LmaxSlashSymbol = "GBP/USD",
            FixSecurityIdSource = Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
            MarketDepth = 1,
            MarketDataMaxWaitSeconds =
                Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds,
            MarketDataRequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MarketDataSymbolEncodingMode =
                LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol
        };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "QQ.Production.Intraday.sln was not found above the test output directory.");
    }

    private sealed class BlockingWriteStream : Stream
    {
        private int activeWrites;

        public int WriteCount { get; private set; }
        public int ActiveWrites => Volatile.Read(ref activeWrites);
        public bool CancellationObserved { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            Interlocked.Increment(ref activeWrites);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref activeWrites);
            }
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();
        public override void SetLength(long value)
            => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
