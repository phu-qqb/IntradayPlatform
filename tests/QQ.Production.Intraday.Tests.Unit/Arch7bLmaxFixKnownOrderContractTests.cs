using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLmaxFixKnownOrderContractTests
{
    [Fact]
    public void OrderCancelRequest_MatchesHistoricalR215Contract()
    {
        var message = LmaxFixRecoveryCodec.BuildOrderCancelRequest(
            "SENDER",
            "LMXBD",
            3,
            "A7BC0123456789ABCDEF",
            "A7BO0123456789ABCDEF",
            "GBPUSD",
            "1",
            0.1m,
            "4002",
            "8");

        Assert.Equal("F", LmaxFixMarketDataCodec.GetTag(message, "35"));
        Assert.Equal("A7BC0123456789ABCDEF", LmaxFixMarketDataCodec.GetTag(message, "11"));
        Assert.Equal("A7BO0123456789ABCDEF", LmaxFixMarketDataCodec.GetTag(message, "41"));
        Assert.Equal("GBPUSD", LmaxFixMarketDataCodec.GetTag(message, "55"));
        Assert.Equal("1", LmaxFixMarketDataCodec.GetTag(message, "54"));
        Assert.Equal("0.1", LmaxFixMarketDataCodec.GetTag(message, "38"));
        Assert.Equal("4002", LmaxFixMarketDataCodec.GetTag(message, "48"));
        Assert.Equal("8", LmaxFixMarketDataCodec.GetTag(message, "22"));
    }

    [Fact]
    public void ExecutionReport_PreservesSequencePossDupAndRawSha256()
    {
        var message = LmaxFixMarketDataCodec.BuildMessage("8", 42, "LMXBD", "SENDER",
        [
            ("43", "Y"),
            ("17", "EXEC-1"),
            ("37", "ORDER-1"),
            ("11", "A7BO0123456789ABCDEF"),
            ("150", "F"),
            ("39", "2"),
            ("48", "4002"),
            ("22", "8"),
            ("55", "GBPUSD"),
            ("54", "1"),
            ("38", "0.1"),
            ("151", "0"),
            ("14", "0.1"),
            ("32", "0.1"),
            ("31", "1.25000"),
            ("6", "1.25000"),
            ("44", "1.25000"),
            ("59", "0"),
            ("40", "2"),
            ("60", "20260723-01:00:00.000"),
            ("1", Arch7bKnownOrderQualificationPolicy.DemoAccountId)
        ]);

        var report = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, null).Report;

        Assert.Equal(42, report.FixSequenceNumber);
        Assert.True(report.PossDup);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(message))).ToLowerInvariant(),
            report.RawMessageSha256);
    }

    [Fact]
    public async Task RawClient_IsDisabledByDefaultWithoutOpeningSocket()
    {
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());

        var result = await client.Arch7bKnownOrderLifecycleAsync(
            LiveLikeOptions(),
            LmaxFixArch7bKnownOrderRequest.Disabled(),
            CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("ARCH7B_EXECUTION_DISABLED_BY_DEFAULT", result.Blocker);
        Assert.False(result.Connected);
        Assert.False(result.OpeningSent);
        Assert.False(result.FlattenSent);
    }

    [Fact]
    public async Task RawClient_DryRunBuildsBoundedDayLifecycleWithoutNetwork()
    {
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var request = Request(LmaxFixArch7bActivation.DryRun);

        var result = await client.Arch7bKnownOrderLifecycleAsync(
            LiveLikeOptions(),
            request,
            CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.False(result.Connected);
        Assert.Contains(result.Diagnostics, value => value.Contains("35=D", StringComparison.Ordinal) && value.Contains("59=0", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, value => value.Contains("35=F", StringComparison.Ordinal) && value.Contains("41=A7BO0123456789ABCDEF", StringComparison.Ordinal));
        Assert.Equal(0, result.OrderStatusRequestCount);
        Assert.Contains(
            "ARCH7B_FLATTEN_DYNAMIC_AFTER_TERMINAL_FRESH_LMAX_BBO_NO_MESSAGE_BUILT",
            result.Diagnostics);
        Assert.Contains("ARCH7B_DRY_RUN_NO_NETWORK_NO_SEND", result.Diagnostics);
    }

    [Fact]
    public void RawClient_CleansUpOrderEntrySessionOnEveryPostLogonExit()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7b.cs"));

        Assert.DoesNotContain("return Result(\"Failed\"", source, StringComparison.Ordinal);
        Assert.Contains("return await ResultWithCleanupAsync(\"Failed\"", source, StringComparison.Ordinal);
        Assert.Contains(
            "await EnsureOrderEntryLogoutAsync(\"ARCH7B_SCOPE_EXIT_CLEANUP\")",
            source,
            StringComparison.Ordinal);

        var marketDataSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.cs"));
        Assert.Contains(
            "logoutSent = await TrySendLogoutAsync(activeStream",
            marketDataSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("logoutSent = true;", marketDataSource, StringComparison.Ordinal);
        Assert.Contains("finally", marketDataSource, StringComparison.Ordinal);
        Assert.Contains(
            "ARCH7B_MARKET_DATA_FIX_SEQUENCE_GAP_UNRESOLVED",
            marketDataSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "await TryUnsubscribeAsync(stream)",
            marketDataSource,
            StringComparison.Ordinal);

        var lifecycleSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7b.cs"));
        Assert.Contains(
            "MaximumFlattenBboAcquisitionAttempts",
            lifecycleSource,
            StringComparison.Ordinal);
        Assert.Contains("remaining < attemptBudget", lifecycleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveValidation_RejectsRealAccountAndMissingExactAuthorization()
    {
        var request = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            AccountId = Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
            ExactOperatorAuthorizationPresent = false
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            LiveLikeOptions(),
            request,
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH", blockers);
        Assert.Contains("ARCH7B_REAL_ACCOUNT_FORBIDDEN", blockers);
        Assert.Contains("ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING", blockers);
        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH", blockers);
    }

    [Fact]
    public void LiveValidation_BindsExactLmaxBboEconomicsIntoAuthorizationPacket()
    {
        var request = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            BboAsk = 1.25001m,
            BboSource = "POLYGON"
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            LiveLikeOptions(),
            request,
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_BBO_SOURCE_NOT_LMAX", blockers);
        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH", blockers);
    }

    [Fact]
    public void Opening_validation_requires_distinct_content_addressed_lmax_observation_contract()
    {
        var request = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            OpeningMarketObservationId = new string('d', 64),
            BboSequenceIntegrityProven = false,
            BboPolygonUsed = true,
            BboSymbol = "EURUSD",
            BboSecurityId = "4001",
            BboAcquisitionStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            LiveLikeOptions(),
            request,
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_BBO_INSTRUMENT_MISMATCH", blockers);
        Assert.Contains("ARCH7B_BBO_SEQUENCE_INTEGRITY_UNPROVEN", blockers);
        Assert.Contains("ARCH7B_POLYGON_ORDER_PRICE_FORBIDDEN", blockers);
        Assert.Contains("ARCH7B_OPENING_MARKET_OBSERVATION_ID_INVALID", blockers);
        Assert.Contains("ARCH7B_BBO_NOT_ACQUIRED_IN_AUTHORIZED_WINDOW", blockers);
    }

    [Theory]
    [InlineData(LmaxFixMarketDataRequestMode.SnapshotOnly)]
    [InlineData(LmaxFixMarketDataRequestMode.Auto)]
    public void Lifecycle_refuses_non_streaming_or_auto_market_data_mode(
        LmaxFixMarketDataRequestMode mode)
    {
        var options = LiveLikeOptions();
        options.MarketDataRequestMode = mode;

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            options,
            Request(LmaxFixArch7bActivation.AuthorizedOnce),
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_MARKET_DATA_SESSION_MODE_UNBOUNDED", blockers);
    }

    [Fact]
    public void Streaming_bbo_aggregates_successive_bid_and_ask_for_one_request()
    {
        var entries = new[]
        {
            Entry("REQ-1", "0", 1.24990m),
            Entry("REQ-1", "1", 1.25000m)
        };

        var top = LmaxFixMarketDataCodec.ComputeBoundedStreamingTopOfBook(
            entries,
            "REQ-1",
            StreamingRequestOptions());

        Assert.True(top.Complete);
        Assert.Equal(1.24990m, top.BestBid);
        Assert.Equal(1.25000m, top.BestAsk);
        Assert.Null(top.Blocker);
    }

    [Fact]
    public void Streaming_bbo_uses_unique_request_identity_when_response_omits_redundant_instrument_tags()
    {
        var entries = new[]
        {
            Entry("REQ-1", "0", 1.24990m) with { Symbol = null, SecurityId = null },
            Entry("REQ-1", "1", 1.25000m) with { Symbol = null, SecurityId = null }
        };

        var top = LmaxFixMarketDataCodec.ComputeBoundedStreamingTopOfBook(
            entries,
            "REQ-1",
            StreamingRequestOptions());

        Assert.True(top.Complete);
        Assert.Equal(1.24990m, top.BestBid);
        Assert.Equal(1.25000m, top.BestAsk);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void Streaming_bbo_refuses_unilateral_book(string entryType)
    {
        var top = LmaxFixMarketDataCodec.ComputeBoundedStreamingTopOfBook(
            [Entry("REQ-1", entryType, 1.25000m)],
            "REQ-1",
            StreamingRequestOptions());

        Assert.False(top.Complete);
        Assert.Equal("ARCH7B_FLATTEN_BBO_BID_ASK_INCOMPLETE", top.Blocker);
    }

    [Fact]
    public void Streaming_bbo_refuses_wrong_request_or_instrument()
    {
        var wrongRequest = LmaxFixMarketDataCodec.ComputeBoundedStreamingTopOfBook(
            [Entry("REQ-2", "0", 1.24990m)],
            "REQ-1",
            StreamingRequestOptions());
        var wrongInstrument = LmaxFixMarketDataCodec.ComputeBoundedStreamingTopOfBook(
            [Entry("REQ-1", "0", 1.24990m) with { SecurityId = "4001" }],
            "REQ-1",
            StreamingRequestOptions());

        Assert.Equal("ARCH7B_FLATTEN_BBO_MDREQID_MISMATCH", wrongRequest.Blocker);
        Assert.Equal("ARCH7B_FLATTEN_BBO_INSTRUMENT_MISMATCH", wrongInstrument.Blocker);
    }

    [Fact]
    public void Streaming_request_unsubscribes_with_the_same_mdreqid()
    {
        var options = StreamingRequestOptions();

        var subscribe = LmaxFixMarketDataCodec.BuildMarketDataRequest(
            "SENDER", "LMXBDM", 2, "REQ-1", options);
        var unsubscribe = LmaxFixMarketDataCodec.BuildMarketDataRequest(
            "SENDER", "LMXBDM", 3, "REQ-1", options, unsubscribe: true);

        Assert.Equal("1", LmaxFixMarketDataCodec.GetTag(subscribe, "263"));
        Assert.Equal("2", LmaxFixMarketDataCodec.GetTag(unsubscribe, "263"));
        Assert.Equal(
            LmaxFixMarketDataCodec.GetTag(subscribe, "262"),
            LmaxFixMarketDataCodec.GetTag(unsubscribe, "262"));
    }

    [Fact]
    public void Flatten_refuses_complete_book_when_unsubscribe_or_logout_is_unproven()
    {
        var now = DateTimeOffset.UtcNow;
        var result = FreshSnapshot(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            1.24990m,
            1.25000m,
            new string('b', 64)) with
        {
            UnsubscribeSent = false,
            UnsubscribeMdReqId = null
        };

        var decision = LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            MarketDataOnlyOptions(),
            result,
            now.AddSeconds(-3),
            now,
            new string('a', 64));

        Assert.False(decision.Allowed);
        Assert.Contains(
            "ARCH7B_FLATTEN_BBO_UNSUBSCRIBE_OR_LOGOUT_UNPROVEN",
            decision.Blockers);
    }

    [Fact]
    public void Session_reject_for_tag_263_is_fail_closed_before_any_order_decision()
    {
        var now = DateTimeOffset.UtcNow;
        var rejected = FreshSnapshot(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            1.24990m,
            1.25000m,
            new string('b', 64)) with
        {
            Status = "Failed",
            MarketDataRejectReceived = true,
            RejectRefTagId = "263",
            RejectRefMsgType = "V",
            SessionRejectReason = "5",
            SanitizedRejectSha256 = new string('c', 64)
        };

        var decision = LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            MarketDataOnlyOptions(),
            rejected,
            now.AddSeconds(-3),
            now,
            new string('a', 64));

        Assert.False(decision.Allowed);
        Assert.Contains(
            "ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH",
            decision.Blockers);
        Assert.Null(decision.LimitPrice);
    }

    [Fact]
    public void Flatten_accepts_same_prices_only_with_fresh_distinct_observation_and_uses_sell_touch()
    {
        var now = DateTimeOffset.UtcNow;
        var openingObservationId = new string('a', 64);
        var result = FreshSnapshot(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            1.24990m,
            1.25000m,
            new string('b', 64));

        var decision =
            LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
                MarketDataOnlyOptions(),
                result,
                now.AddSeconds(-3),
                now,
                openingObservationId);

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.Equal(1.24990m, decision.LimitPrice);
        Assert.Equal(new string('b', 64), decision.Observation!.SnapshotSha256);
        Assert.NotEqual(openingObservationId, decision.Observation.SnapshotSha256);
    }

    [Fact]
    public void Flatten_rejects_recycled_stale_preterminal_or_sequence_unproven_observation()
    {
        var now = DateTimeOffset.UtcNow;
        var openingObservationId = new string('a', 64);
        var recycled = FreshSnapshot(
            now.AddSeconds(-8),
            now.AddSeconds(-7),
            1.24990m,
            1.25000m,
            openingObservationId) with
        {
            InboundSequenceIntegrityProven = false
        };

        var decision =
            LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
                MarketDataOnlyOptions(),
                recycled,
                now.AddSeconds(-2),
                now,
                openingObservationId);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_FLATTEN_BBO_SEQUENCE_INTEGRITY_UNPROVEN", decision.Blockers);
        Assert.Contains("ARCH7B_FLATTEN_BBO_NOT_POST_OPENING_TERMINAL", decision.Blockers);
        Assert.Contains("ARCH7B_FLATTEN_BBO_STALE", decision.Blockers);
        Assert.Contains("ARCH7B_FLATTEN_MARKET_OBSERVATION_ID_NOT_DISTINCT", decision.Blockers);
    }

    [Fact]
    public void Flatten_without_fresh_lmax_bbo_is_kill_switch_blocked_without_polygon_fallback()
    {
        var now = DateTimeOffset.UtcNow;
        var unavailable = LmaxFixMarketDataSmokeResult.Skipped("no snapshot", []);

        var decision =
            LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
                MarketDataOnlyOptions(),
                unavailable,
                now.AddSeconds(-1),
                now,
                new string('a', 64));

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH", decision.Blockers);
        Assert.Null(decision.LimitPrice);
        Assert.Null(decision.Observation);
    }

    [Fact]
    public void Touch_limits_are_side_correct_tick_aligned_and_spread_bounded()
    {
        var observation = new Arch7bLmaxBbo(
            "GBPUSD",
            "4002",
            1.24990m,
            1.25000m,
            DateTimeOffset.UtcNow,
            "LMAX",
            new string('a', 64),
            DateTimeOffset.UtcNow.AddMilliseconds(-1),
            SequenceIntegrityProven: true);

        Assert.Equal(1.25000m, Arch7bKnownOrderQualification.TouchLimit(observation, "BUY"));
        Assert.Equal(1.24990m, Arch7bKnownOrderQualification.TouchLimit(observation, "SELL"));
        Assert.Equal(
            "ARCH7B_BBO_NOT_TICK_ALIGNED",
            Assert.Throws<InvalidOperationException>(() =>
                Arch7bKnownOrderQualification.TouchLimit(
                    observation with { Bid = 1.249901m },
                    "SELL")).Message);
        Assert.Equal(
            "ARCH7B_BBO_SPREAD_TOO_WIDE",
            Assert.Throws<InvalidOperationException>(() =>
                Arch7bKnownOrderQualification.TouchLimit(
                    observation with { Ask = 1.25020m },
                    "SELL")).Message);
    }
    [Fact]
    public void Restart_after_open_send_before_ack_queries_known_order_without_resend()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            openingCum: 0m,
            openingLeaves: 0m,
            openingTerminal: false));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.True(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_partial_fill_and_cancel_send_never_resends_cancel()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            cancelSent: true,
            openingCum: 0.04m,
            openingLeaves: 0.06m,
            openingTerminal: false));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.False(plan.MaySendOpeningResidualCancel);
        Assert.True(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_open_fill_before_flatten_allows_only_first_flatten()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            openingCum: 0.1m,
            openingLeaves: 0m,
            openingTerminal: true));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.True(plan.MaySendFlattenNewOrderSingle);
        Assert.False(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_flatten_send_queries_known_flatten_without_resend()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            flattenSent: true,
            openingCum: 0.1m,
            openingLeaves: 0m,
            openingTerminal: true,
            flattenCum: 0m,
            flattenLeaves: 0.1m,
            flattenTerminal: false));

        Assert.False(plan.MaySendFlattenNewOrderSingle);
        Assert.True(plan.QueryFlattenKnownOrder);
    }

    private static LmaxFixArch7bRecoveryState State(
        bool openingSent = false,
        bool cancelSent = false,
        bool flattenSent = false,
        int statusRequestCount = 0,
        decimal openingCum = 0m,
        decimal openingLeaves = 0m,
        bool openingTerminal = false,
        decimal flattenCum = 0m,
        decimal flattenLeaves = 0m,
        bool flattenTerminal = false)
        => new(
            openingSent,
            cancelSent,
            flattenSent,
            statusRequestCount,
            openingCum,
            openingLeaves,
            openingTerminal,
            flattenCum,
            flattenLeaves,
            flattenTerminal);

    private static LmaxFixArch7bKnownOrderRequest Request(LmaxFixArch7bActivation activation)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new LmaxFixArch7bKnownOrderRequest(
            activation,
            Guid.Parse("a7000000-0000-0000-0000-000000000001"),
            Guid.Parse("a7000000-0000-0000-0000-000000000002"),
            "arch7b-test-session",
            "arch7b-test-owner",
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            "A7BO0123456789ABCDEF",
            "A7BC0123456789ABCDEF",
            "A7BF0123456789ABCDEF",
            1.25000m,
            1.24980m,
            1.25020m,
            1.24990m,
            1.25000m,
            now,
            "LMAX",
            new string('b', 64),
            now,
            now.AddSeconds(30),
            now.AddSeconds(120),
            new string('c', 64),
            new string('a', 64),
            true,
            true,
            true,
            false);
        request = request with
        {
            OpeningMarketObservationId = request.BboSnapshotSha256,
            BboSymbol = Arch7bKnownOrderQualificationPolicy.Symbol,
            BboSecurityId = Arch7bKnownOrderQualificationPolicy.SecurityId,
            BboAcquisitionStartedAtUtc = now,
            BboSequenceIntegrityProven = true,
            BboPolygonUsed = false
        };
        return request with
        {
            AuthorizationPacketSha256 =
                LmaxFixArch7bKnownOrderContract.ComputeAuthorizationPacketSha256(request)
        };
    }

    private static LmaxFixMarketDataSmokeResult FreshSnapshot(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        decimal bid,
        decimal ask,
        string snapshotSha256)
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
            logoutSent: true,
            rejectReason: null,
            rejectText: null,
            lastReceivedMsgType: "W",
            safetyDecisions: [],
            diagnostics: [],
            attempts: [],
            entries: [],
            bestBid: bid,
            bestAsk: ask,
            mid: (bid + ask) / 2m,
            messageCount: 2);
        return result with
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            InboundSequenceIntegrityProven = true,
            SnapshotSha256 = snapshotSha256,
            RequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MdReqId = "REQ-1",
            UnsubscribeSent = true,
            UnsubscribeMdReqId = "REQ-1",
            CompleteTopOfBook = true,
            ObservationCompletedAtUtc = completedAtUtc
        };
    }
    private static LmaxFixMarketDataEntry Entry(
        string mdReqId,
        string entryType,
        decimal price)
        => new(
            mdReqId,
            LmaxFixMarketDataMessageType.IncrementalRefresh,
            "GBP/USD",
            Arch7bKnownOrderQualificationPolicy.SecurityId,
            entryType,
            price,
            1m,
            null,
            null,
            "1");

    private static LmaxFixMarketDataRequestOptions StreamingRequestOptions()
        => LmaxFixMarketDataRequestOptions.FromLabOptions(LiveLikeOptions());

    private static LmaxConnectivityLabOptions MarketDataOnlyOptions()
    {
        var options = LiveLikeOptions();
        options.AllowOrderSubmission = false;
        return options;
    }

    private static LmaxConnectivityLabOptions LiveLikeOptions() =>
        new()
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false,
            FixOrderTargetCompId = "LMXBD",
            FixSenderCompId = "SENDER",
            InstrumentSymbol = Arch7bKnownOrderQualificationPolicy.Symbol,
            LmaxInstrumentId = Arch7bKnownOrderQualificationPolicy.SecurityId,
            LmaxSlashSymbol = "GBP/USD",
            FixSecurityIdSource = Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
            MarketDepth = 1,
            MarketDataMaxWaitSeconds =
                Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds,
            MarketDataRequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MarketDataSymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol
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
