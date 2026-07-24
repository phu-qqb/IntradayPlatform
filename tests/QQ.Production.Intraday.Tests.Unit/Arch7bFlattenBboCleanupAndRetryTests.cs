using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bFlattenBboCleanupAndRetryTests
{
    [Fact]
    public void Valid_bbo_with_complete_cleanup_is_accepted()
    {
        var now = DateTimeOffset.UtcNow;
        var result = Observation(now.AddSeconds(-2), now.AddSeconds(-1));

        var decision = Evaluate(result, now.AddSeconds(-3), now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.True(result.UnsubscribeAttempted);
        Assert.True(result.UnsubscribeSent);
        Assert.Equal(result.MdReqId, result.UnsubscribeMdReqId);
        Assert.True(result.LogoutAttempted);
        Assert.True(result.LogoutSent);
        Assert.True(result.StreamDisposeAttempted);
        Assert.True(result.StreamDisposeSucceeded);
        Assert.True(result.SocketDisposeAttempted);
        Assert.True(result.SocketDisposeSucceeded);
    }

    [Fact]
    public void Valid_bbo_with_unsubscribe_failure_still_authorizes_flatten()
    {
        var now = DateTimeOffset.UtcNow;
        var result = Observation(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            unsubscribeSent: false,
            cleanupDiagnostics:
            [
                "ARCH7B_MARKET_DATA_UNSUBSCRIBE_FAILURE:IOException"
            ]);

        var decision = Evaluate(result, now.AddSeconds(-3), now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.DoesNotContain(
            "ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH",
            decision.Blockers);
        Assert.True(result.UnsubscribeAttempted);
        Assert.False(result.UnsubscribeSent);
        Assert.Equal(result.MdReqId, result.UnsubscribeMdReqId);
        Assert.Contains(
            "ARCH7B_MARKET_DATA_UNSUBSCRIBE_FAILURE:IOException",
            result.CleanupDiagnostics);
        Assert.True(result.StreamDisposeSucceeded);
        Assert.True(result.SocketDisposeSucceeded);
    }

    [Fact]
    public void Valid_bbo_with_logout_failure_still_authorizes_flatten()
    {
        var now = DateTimeOffset.UtcNow;
        var result = Observation(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            logoutSent: false,
            cleanupDiagnostics:
            [
                "ARCH7B_MARKET_DATA_LOGOUT_FAILURE:SANITIZED"
            ]);

        var decision = Evaluate(result, now.AddSeconds(-3), now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.DoesNotContain(
            "ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH",
            decision.Blockers);
        Assert.True(result.LogoutAttempted);
        Assert.False(result.LogoutSent);
        Assert.Contains(
            "ARCH7B_MARKET_DATA_LOGOUT_FAILURE:SANITIZED",
            result.CleanupDiagnostics);
        Assert.True(result.StreamDisposeSucceeded);
        Assert.True(result.SocketDisposeSucceeded);
    }

    [Fact]
    public void Successful_cleanup_never_makes_an_invalid_book_acceptable()
    {
        var now = DateTimeOffset.UtcNow;
        var result = Observation(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            bid: 1.25010m,
            ask: 1.25000m);

        var decision = Evaluate(result, now.AddSeconds(-3), now);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_FLATTEN_BBO_INVALID", decision.Blockers);
        Assert.Null(decision.LimitPrice);
    }

    [Fact]
    public void First_attempt_with_fresh_observation_is_accepted()
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-3);
        var result = Observation(now.AddSeconds(-2), now.AddSeconds(-1));

        var decision = Evaluate(result, notBefore, now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
    }

    [Fact]
    public void Second_attempt_started_seven_seconds_after_terminal_is_accepted_when_observation_is_fresh()
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-9);
        var result = Observation(now.AddSeconds(-2), now.AddSeconds(-1));

        var decision = Evaluate(result, notBefore, now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.True(result.StartedAtUtc - notBefore > TimeSpan.FromSeconds(5));
        Assert.True(now - result.ObservationCompletedAtUtc!.Value <= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Third_attempt_started_more_than_ten_seconds_after_terminal_is_accepted_when_observation_is_fresh()
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-13);
        var result = Observation(now.AddSeconds(-2), now.AddSeconds(-1));

        var decision = Evaluate(result, notBefore, now);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.True(result.StartedAtUtc - notBefore > TimeSpan.FromSeconds(10));
        Assert.True(now - result.ObservationCompletedAtUtc!.Value <= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Retry_observation_older_than_five_seconds_is_rejected_as_stale()
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-20);
        var result = Observation(now.AddSeconds(-8), now.AddSeconds(-6));

        var decision = Evaluate(result, notBefore, now);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_FLATTEN_BBO_STALE", decision.Blockers);
    }

    [Theory]
    [InlineData(-4, -1)]
    [InlineData(1, -1)]
    public void Observation_or_session_before_opening_terminal_is_rejected(
        int startedOffsetSeconds,
        int observedOffsetSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddSeconds(-3);
        var result = Observation(
            notBefore.AddSeconds(startedOffsetSeconds),
            notBefore.AddSeconds(observedOffsetSeconds));

        var decision = Evaluate(result, notBefore, now);

        Assert.False(decision.Allowed);
        Assert.Contains(
            "ARCH7B_FLATTEN_BBO_NOT_POST_OPENING_TERMINAL",
            decision.Blockers);
    }

    [Fact]
    public void Runner_keeps_exact_three_attempts_five_second_attempt_budget_and_global_deadline()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7b.cs"));

        Assert.Equal(
            3,
            Arch7bKnownOrderQualificationPolicy.MaximumFlattenBboAcquisitionAttempts);
        Assert.Contains(
            "attempt <= Arch7bKnownOrderQualificationPolicy.MaximumFlattenBboAcquisitionAttempts",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "DateTimeOffset.UtcNow < request.DeadlineUtc",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MaximumBboAgeSeconds",
            source,
            StringComparison.Ordinal);
        Assert.Contains("remaining < attemptBudget", source, StringComparison.Ordinal);
        Assert.DoesNotContain("attempt <= 4", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_cleanup_uses_one_bounded_deadline_and_disposes_in_finally()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.cs"));
        var cleanupSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "LmaxFixMarketDataCleanup.cs"));

        Assert.Contains("finally", source, StringComparison.Ordinal);
        Assert.Contains("LmaxFixMarketDataCleanup.RunAsync", source, StringComparison.Ordinal);
        Assert.Contains("cleanupDeadlineUtc", source, StringComparison.Ordinal);
        Assert.Contains(
            "MaximumMarketDataCleanupMilliseconds",
            source,
            StringComparison.Ordinal);
        Assert.Contains("tcp.Client.Close(0)", source, StringComparison.Ordinal);
        Assert.Contains("stream.Dispose", source, StringComparison.Ordinal);
        Assert.Contains("CreateLinkedTokenSource", cleanupSource, StringComparison.Ordinal);
        Assert.Contains("unsubscribeSlice", cleanupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CancellationToken.None", cleanupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", cleanupSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "boundedBook.Complete && unsubscribeSent && logoutSent",
            source,
            StringComparison.Ordinal);
    }

    private static LmaxFixArch7bMarketObservationDecision Evaluate(
        LmaxFixMarketDataSmokeResult result,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset nowUtc)
        => LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            MarketDataOptions(),
            result,
            notBeforeUtc,
            nowUtc,
            new string('a', 64));

    private static LmaxFixMarketDataSmokeResult Observation(
        DateTimeOffset startedAtUtc,
        DateTimeOffset observedAtUtc,
        decimal bid = 1.24990m,
        decimal ask = 1.25000m,
        bool unsubscribeSent = true,
        bool logoutSent = true,
        IReadOnlyList<string>? cleanupDiagnostics = null)
    {
        var result = LmaxFixMarketDataSmokeResult.Create(
            "Ok",
            "test",
            startedAtUtc,
            tcpConnected: true,
            tlsHandshakeCompleted: true,
            fixLogonSent: true,
            fixLoggedOn: true,
            marketDataRequestSent: true,
            marketDataSnapshotReceived: true,
            marketDataRejectReceived: false,
            logoutSent: logoutSent,
            rejectReason: null,
            rejectText: null,
            lastReceivedMsgType: "X",
            safetyDecisions: [],
            diagnostics: [],
            attempts: [],
            bestBid: bid,
            bestAsk: ask,
            mid: (bid + ask) / 2m,
            messageCount: 2);
        return result with
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = observedAtUtc,
            ObservationCompletedAtUtc = observedAtUtc,
            InboundSequenceIntegrityProven = true,
            SnapshotSha256 = new string('b', 64),
            RequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MdReqId = "REQ-1",
            CompleteTopOfBook = true,
            Cleanup = new LmaxFixMarketDataCleanupSnapshot(
                unsubscribeAttempted: true,
                unsubscribeSent: unsubscribeSent,
                unsubscribeMdReqId: "REQ-1",
                logoutAttempted: true,
                logoutSent: logoutSent,
                streamDisposeAttempted: true,
                streamDisposeSucceeded: true,
                socketDisposeAttempted: true,
                socketDisposeSucceeded: true,
                forceCloseAttempted: true,
                forceCloseSucceeded: true,
                startedAtUtc: startedAtUtc,
                completedAtUtc: observedAtUtc,
                deadlineUtc: observedAtUtc.AddSeconds(1),
                diagnostics: cleanupDiagnostics)
        };
    }

    private static LmaxConnectivityLabOptions MarketDataOptions() =>
        new()
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
            if (File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "QQ.Production.Intraday.sln was not found above the test output directory.");
    }
}
