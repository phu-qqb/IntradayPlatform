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
    public async Task RawClient_ProductionDryRunUsesTheBoundedProductionProfileWithoutNetwork()
    {
        var binding = ProductionBinding();
        var request = WithProductionPacket(ProductionRequest(binding) with
        {
            Activation = LmaxFixArch7bActivation.ProductionDryRun,
            ProductionCommandConfirmed = false
        });
        var options = ProductionOptions(binding);
        options.AllowExternalConnections = false;
        options.AllowOrderSubmission = false;
        options.DryRun = true;
        options.FixUsername = null;
        options.FixPassword = null;
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());

        var result = await client.Arch7bKnownOrderLifecycleAsync(
            options, request, CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.False(result.Connected);
        Assert.False(result.OpeningSent);
        Assert.False(result.FlattenSent);
        Assert.Contains(result.Diagnostics, value => value.Contains("55=EURUSD", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, value => value.Contains("38=0.2", StringComparison.Ordinal));
        Assert.Contains("ARCH7B_DRY_RUN_NO_NETWORK_NO_SEND", result.Diagnostics);
    }

    [Fact]
    public async Task ProductionReadiness_MissingDedicatedConfirmationOpensNeitherDatabaseNorSocket()
    {
        var binding = ProductionBinding();
        var options = ProductionOptions(binding);
        options.AllowOrderSubmission = false;
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());

        var result = await client.Arch7bProductionReadinessAsync(
            options,
            ReadinessBinding(binding),
            explicitReadinessConfirmation: false,
            CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("ARCH7B_PRODUCTION_READINESS_CLI_CONFIRMATION_MISSING", result.Blocker);
        Assert.False(result.Persistence.Connected);
        Assert.False(result.MarketData.TcpConnected);
        Assert.False(result.OrderEntry.TcpConnected);
    }

    [Fact]
    public async Task ProductionReadiness_ValidateOnlyUsesBindingAndPerformsZeroIo()
    {
        var binding = ReadinessBinding(ProductionBinding());
        var options = ProductionOptions(ProductionBinding());
        options.AllowExternalConnections = false;
        options.AllowOrderSubmission = false;
        options.AllowLiveTrading = false;
        options.DryRun = true;
        var connectionSetting = Arch7bPostgreSqlPersistenceTarget.ProductionConnectionEnvironmentVariable;
        var original = Environment.GetEnvironmentVariable(connectionSetting);
        Environment.SetEnvironmentVariable(
            connectionSetting,
            $"Host={binding.PersistenceHost};Port={binding.PersistencePort};Database={binding.PersistenceDatabase}");
        try
        {
            var result = await new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator())
                .Arch7bProductionReadinessValidateOnlyAsync(
                    options, binding, explicitReadinessConfirmation: true, CancellationToken.None);

            Assert.Equal("Ok", result.Status);
            Assert.True(result.ValidateOnly);
            Assert.True(result.ZeroIo);
            Assert.False(result.Persistence.Connected);
            Assert.False(result.MarketData.TcpConnected);
            Assert.False(result.OrderEntry.TcpConnected);
            Assert.Contains("ARCH7B_PRODUCTION_READINESS_VALIDATE_ONLY_ZERO_IO", result.Diagnostics);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionSetting, original);
        }
    }

    [Fact]
    public async Task ProductionReadiness_MissingFixCredentialFailsBeforeDatabaseOrSocket()
    {
        var binding = ReadinessBinding(ProductionBinding());
        var options = ProductionOptions(ProductionBinding());
        options.AllowOrderSubmission = false;
        options.FixPassword = null;

        var result = await new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator())
            .Arch7bProductionReadinessAsync(
                options, binding, explicitReadinessConfirmation: true, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("QQ_LMAX_FIX_PASSWORD", result.Blocker);
        Assert.True(result.ZeroIo);
    }

    [Fact]
    public void ProductionReadinessBinding_ContainsNoTradingPacketFields()
    {
        var names = typeof(LmaxFixArch7bProductionReadinessBinding)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(names, value => value.Contains("Bbo", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Opening", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Cancel", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Flatten", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("ClientOrder", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Quantity", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Price", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("Policy", StringComparison.Ordinal));
        Assert.DoesNotContain(names, value => value.Contains("AuthorizationPacket", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("account")]
    [InlineData("order-host")]
    [InlineData("market-data-host")]
    [InlineData("market-data-port")]
    [InlineData("market-data-target")]
    [InlineData("order-port")]
    [InlineData("order-target")]
    [InlineData("tls")]
    [InlineData("credentials")]
    [InlineData("instrument")]
    [InlineData("persistence")]
    [InlineData("external-disabled")]
    [InlineData("orders")]
    [InlineData("live")]
    public void ProductionReadiness_RejectsUnsafeOrMismatchedInputsBeforeExternalOperations(string scenario)
    {
        var binding = ProductionBinding();
        var readinessBinding = ReadinessBinding(binding);
        var options = ProductionOptions(binding);
        options.AllowOrderSubmission = false;
        switch (scenario)
        {
            case "environment": options.EnvironmentName = "Demo"; break;
            case "account": options.AccountCode = "wrong"; break;
            case "order-host": options.FixOrderHost = "wrong.example"; break;
            case "market-data-host": options.FixMarketDataHost = "wrong.example"; break;
            case "market-data-port": options.FixMarketDataPort = 1; break;
            case "market-data-target": options.FixMarketDataTargetCompId = "wrong"; break;
            case "order-port": options.FixOrderPort = 1; break;
            case "order-target": options.FixOrderTargetCompId = "wrong"; break;
            case "tls": options.UseTls = false; break;
            case "credentials": options.FixPassword = null; break;
            case "instrument": options.InstrumentSymbol = "wrong"; break;
            case "persistence": readinessBinding = readinessBinding with { PersistenceHost = string.Empty }; break;
            case "external-disabled": options.AllowExternalConnections = false; break;
            case "orders": options.AllowOrderSubmission = true; break;
            case "live": options.AllowLiveTrading = true; break;
        }

        var blockers = LmaxFixArch7bProductionReadinessContract.Validate(
            options,
            readinessBinding,
            explicitReadinessConfirmation: true,
            validateOnly: false,
            nowUtc: DateTimeOffset.UtcNow);

        Assert.NotEmpty(blockers);
        Assert.DoesNotContain("ARCH7B_PRODUCTION_READINESS_CLI_CONFIRMATION_MISSING", blockers);
    }

    [Fact]
    public void ProductionReadiness_SourceHasNoKnownOrderOrTradingMessagePath_AndPersistenceSqlIsReadOnly()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7bProductionReadiness.cs"));

        Assert.DoesNotContain("Arch7bKnownOrderLifecycleAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxFixArch7bKnownOrderRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildNewOrderSingle", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildOrderCancelRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildOrderStatusRequest", source, StringComparison.Ordinal);
        Assert.Contains("BuildMessage(\"A\"", source, StringComparison.Ordinal);
        Assert.Contains("TrySendLogoutAsync", source, StringComparison.Ordinal);
        Assert.Contains("logonConfirmation.NextOutboundSequenceNumber", source, StringComparison.Ordinal);
        Assert.Contains("return (true, sequenceNumber)", source, StringComparison.Ordinal);
        var revalidation = source.IndexOf(
            "var orderEntryBlockers = LmaxFixArch7bProductionReadinessContract.Validate(",
            StringComparison.Ordinal);
        var orderEntry = source.IndexOf(
            "ValidateOrderEntryLogonReadinessAsync(", StringComparison.Ordinal);
        Assert.True(revalidation >= 0 && revalidation < orderEntry);
        Assert.Contains("CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("CreateTlsStreamAsync(tcp, options.FixOrderHost!, tlsTimeout.Token)", source, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", source, StringComparison.Ordinal);
        Assert.Contains("SELECT 1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ", source, StringComparison.Ordinal);

        var runnerSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "LabRunner.cs"));
        var readinessCommand = runnerSource.IndexOf(
            "if (command.Equals(\"fix-arch7b-production-readiness\"", StringComparison.Ordinal);
        var readinessBlock = runnerSource[readinessCommand..];
        Assert.Contains("readiness-binding-json", readinessBlock, StringComparison.Ordinal);
        Assert.Contains("validate-only", readinessBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("request-json", readinessBlock[..readinessBlock.IndexOf(
            "if (command.Equals(\"fix-order-mass-status-smoke\"", StringComparison.Ordinal)], StringComparison.Ordinal);

        var runbook = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "docs",
            "LMAX_ARCH7B_FIRST_PRODUCTION_CANARY_RUNBOOK.md"));
        var gateOne = runbook.IndexOf("## GATE 1 — readiness validate-only", StringComparison.Ordinal);
        var gateTwo = runbook.IndexOf("## GATE 2 — production readiness", StringComparison.Ordinal);
        var gateFour = runbook.IndexOf("## GATE 4 — fresh trading packet and ProductionDryRun", StringComparison.Ordinal);
        Assert.True(gateOne >= 0 && gateOne < gateTwo && gateTwo < gateFour);
        Assert.Contains("--readiness-binding-json", runbook, StringComparison.Ordinal);
        Assert.Contains("ZeroIo=true", runbook, StringComparison.Ordinal);
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
        Assert.DoesNotContain("logoutSent = true;", marketDataSource, StringComparison.Ordinal);
        Assert.Contains("finally", marketDataSource, StringComparison.Ordinal);
        Assert.Contains(
            "ARCH7B_MARKET_DATA_FIX_SEQUENCE_GAP_UNRESOLVED",
            marketDataSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Func<CancellationToken, Task<bool>>? unsubscribeAsync",
            marketDataSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Func<CancellationToken, Task<bool>>? logoutAsync",
            marketDataSource,
            StringComparison.Ordinal);

        var lifecycleSource = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7b.cs"));
        Assert.Contains(
            "request.DeadlineUtc",
            lifecycleSource,
            StringComparison.Ordinal);
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
    public void Flatten_accepts_complete_book_when_cleanup_is_imperfect()
    {
        var now = DateTimeOffset.UtcNow;
        var result = FreshSnapshot(
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            1.24990m,
            1.25000m,
            new string('b', 64)) with
        {
            LogoutSent = false,
            Cleanup = new LmaxFixMarketDataCleanupSnapshot(
                unsubscribeAttempted: true,
                unsubscribeSent: false,
                unsubscribeMdReqId: "REQ-1",
                logoutAttempted: true,
                logoutSent: false,
                streamDisposeAttempted: true,
                streamDisposeSucceeded: true,
                socketDisposeAttempted: true,
                socketDisposeSucceeded: true,
                diagnostics:
                [
                    "ARCH7B_MARKET_DATA_UNSUBSCRIBE_FAILURE:IOException",
                    "ARCH7B_MARKET_DATA_LOGOUT_FAILURE:SANITIZED"
                ])
        };

        var decision = LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            MarketDataOnlyOptions(),
            result,
            now.AddSeconds(-3),
            now,
            new string('a', 64));

        Assert.True(decision.Allowed, string.Join(";", decision.Blockers));
        Assert.DoesNotContain("ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH", decision.Blockers);
        Assert.Equal(1.24990m, decision.LimitPrice);
        Assert.True(result.UnsubscribeAttempted);
        Assert.False(result.UnsubscribeSent);
        Assert.True(result.LogoutAttempted);
        Assert.False(result.LogoutSent);
        Assert.True(result.StreamDisposeSucceeded);
        Assert.True(result.SocketDisposeSucceeded);
        Assert.Equal(2, result.CleanupDiagnostics.Count);
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

    [Fact]
    public void Production_activation_is_separate_and_binds_confirmation_packet_endpoint_and_quantity()
    {
        var binding = ProductionBinding();
        var request = ProductionRequest(binding);
        var options = ProductionOptions(binding);

        Assert.Empty(LmaxFixArch7bKnownOrderContract.Validate(options, request, DateTimeOffset.UtcNow));

        var noConfirmation = request with { ProductionCommandConfirmed = false };
        Assert.Contains("ARCH7B_PRODUCTION_CLI_CONFIRMATION_MISSING",
            LmaxFixArch7bKnownOrderContract.Validate(options, noConfirmation, DateTimeOffset.UtcNow));

        var wrongEndpoint = ProductionOptions(binding);
        wrongEndpoint.FixOrderTargetCompId = "OTHER";
        Assert.Contains("ARCH7B_PRODUCTION_FIX_ENDPOINT_BINDING_MISMATCH",
            LmaxFixArch7bKnownOrderContract.Validate(wrongEndpoint, request, DateTimeOffset.UtcNow));

        var excessive = binding with { VenueQuantity = 1.1m };
        var excessiveRequest = ProductionRequest(excessive);
        Assert.Contains("ARCH7B_PRODUCTION_QUANTITY_CAP_INVALID",
            LmaxFixArch7bKnownOrderContract.Validate(ProductionOptions(excessive), excessiveRequest, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Demo_activation_remains_demo_only_when_production_values_are_present()
    {
        var binding = ProductionBinding();
        var request = ProductionRequest(binding) with
        {
            Activation = LmaxFixArch7bActivation.AuthorizedOnce
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            ProductionOptions(binding), request, DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_FIX_ENVIRONMENT_NOT_DEMO_OR_UAT", blockers);
        Assert.Contains("ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH", blockers);
    }

    [Fact]
    public void Persistence_target_is_selected_from_activation_not_the_production_confirmation()
    {
        var demo = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            ProductionCommandConfirmed = true
        };
        var production = ProductionRequest(ProductionBinding());

        Assert.Equal(Arch7bPostgreSqlPersistenceTarget.DemoConnectionEnvironmentVariable,
            Arch7bPostgreSqlPersistenceTarget.ConnectionEnvironmentVariable(demo));
        Assert.Equal(Arch7bPostgreSqlPersistenceTarget.ProductionConnectionEnvironmentVariable,
            Arch7bPostgreSqlPersistenceTarget.ConnectionEnvironmentVariable(production));
        Assert.Throws<InvalidOperationException>(() =>
            Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(
                production, "localhost", 5432, Arch7bPostgreSqlPersistenceTarget.DemoDatabase));
    }

    [Fact]
    public void Production_persistence_target_requires_the_exact_packet_bound_host_port_and_database()
    {
        var request = ProductionRequest(ProductionBinding());

        Assert.Throws<InvalidOperationException>(() =>
            Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(request, "other-host", 5432,
                request.ProductionBinding!.PersistenceDatabase));
        Assert.Throws<InvalidOperationException>(() =>
            Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(request,
                request.ProductionBinding!.PersistenceHost, 5433, request.ProductionBinding.PersistenceDatabase));
        Assert.Throws<InvalidOperationException>(() =>
            Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(request,
                request.ProductionBinding!.PersistenceHost, request.ProductionBinding.PersistencePort, "other-database"));
    }

    [Fact]
    public void Production_rejects_invalid_time_and_bbo_observation_windows_before_connection()
    {
        var binding = ProductionBinding();
        var now = DateTimeOffset.UtcNow;
        var request = ProductionRequest(binding);
        var options = ProductionOptions(binding);

        Assert.Contains("ARCH7B_OPENING_CANCEL_DEADLINE_NOT_BEFORE_FINAL_DEADLINE",
            LmaxFixArch7bKnownOrderContract.Validate(options,
                WithProductionPacket(request with { OpeningCancelAtUtc = request.DeadlineUtc }), now));
        Assert.Contains("ARCH7B_OPENING_CANCEL_DEADLINE_EXCEEDED",
            LmaxFixArch7bKnownOrderContract.Validate(options,
                WithProductionPacket(request with { OpeningCancelAtUtc = now }), now));
        Assert.Contains("ARCH7B_BBO_NOT_ACQUIRED_IN_AUTHORIZED_WINDOW",
            LmaxFixArch7bKnownOrderContract.Validate(options,
                WithProductionPacket(request with { BboObservedAtUtc = now.AddSeconds(1) }), now));
        Assert.Contains("ARCH7B_OPENING_MARKET_OBSERVATION_ID_INVALID",
            LmaxFixArch7bKnownOrderContract.Validate(options,
                WithProductionPacket(request with { OpeningMarketObservationId = new string('d', 64) }), now));
    }

    [Theory]
    [InlineData("polygon")]
    [InlineData("sequence")]
    [InlineData("cancel")]
    [InlineData("exclusivity")]
    [InlineData("operator")]
    public void Production_authorization_packet_invalidates_when_a_bound_safety_field_changes(string mutation)
    {
        var binding = ProductionBinding();
        var request = ProductionRequest(binding);
        var mutated = mutation switch
        {
            "polygon" => request with { BboPolygonUsed = true },
            "sequence" => request with { BboSequenceIntegrityProven = false },
            "cancel" => request with { OpeningCancelAtUtc = request.OpeningCancelAtUtc.AddSeconds(-1) },
            "exclusivity" => request with { ExclusivityDeclared = false },
            "operator" => request with { ExactOperatorAuthorizationPresent = false },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH",
            LmaxFixArch7bKnownOrderContract.Validate(ProductionOptions(binding), mutated, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Production_fails_closed_for_invalid_increment_tls_and_sender_without_throwing()
    {
        var binding = ProductionBinding();
        var invalidIncrement = binding with { QuantityIncrement = 0m };
        var request = ProductionRequest(invalidIncrement);

        var incrementBlockers = LmaxFixArch7bKnownOrderContract.Validate(
            ProductionOptions(invalidIncrement), request, DateTimeOffset.UtcNow);
        Assert.Contains("ARCH7B_PRODUCTION_QUANTITY_INCREMENT_INVALID", incrementBlockers);

        var noTls = ProductionOptions(binding);
        noTls.UseTls = false;
        Assert.Contains("ARCH7B_PRODUCTION_FIX_TLS_REQUIRED",
            LmaxFixArch7bKnownOrderContract.Validate(noTls, ProductionRequest(binding), DateTimeOffset.UtcNow));

        var wrongSender = ProductionOptions(binding);
        wrongSender.FixSenderCompId = "OTHER";
        Assert.Contains("ARCH7B_PRODUCTION_FIX_SENDER_BINDING_MISMATCH",
            LmaxFixArch7bKnownOrderContract.Validate(wrongSender, ProductionRequest(binding), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Production_dry_run_validates_the_packet_and_remains_zero_network_and_zero_database()
    {
        var binding = ProductionBinding();
        var request = WithProductionPacket(ProductionRequest(binding) with
        {
            Activation = LmaxFixArch7bActivation.ProductionDryRun,
            ProductionCommandConfirmed = false
        });
        var options = ProductionOptions(binding);
        options.AllowExternalConnections = false;
        options.AllowOrderSubmission = false;
        options.DryRun = true;
        options.FixUsername = null;
        options.FixPassword = null;

        Assert.Empty(LmaxFixArch7bKnownOrderContract.Validate(options, request, DateTimeOffset.UtcNow));
        var result = await new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator())
            .Arch7bKnownOrderLifecycleAsync(options, request, CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.False(result.Connected);
        Assert.False(result.OpeningSent);
        Assert.False(result.FlattenSent);
    }

    [Fact]
    public void Authorized_production_read_only_flatten_market_data_is_the_only_specialized_path()
    {
        var binding = ProductionBinding();
        var request = ProductionRequest(binding);
        var readOnly = ProductionOptions(binding);
        readOnly.AllowOrderSubmission = false;

        Assert.Empty(LmaxFixArch7bKnownOrderContract.ValidateProductionReadOnlyMarketData(
            readOnly, request, request.DeadlineUtc, DateTimeOffset.UtcNow));
        Assert.Contains("FIX logon smoke is allowed only in Demo or UAT environments.",
            new LmaxConnectivityLabSafetyValidator().ValidateForFixLogon(readOnly, marketData: true));
    }

    [Fact]
    public async Task Generic_market_data_snapshot_remains_blocked_for_production_without_connecting()
    {
        var options = ProductionOptions(ProductionBinding());
        options.AllowOrderSubmission = false;

        var result = await new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator())
            .MarketDataSnapshotSmokeAsync(options, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.False(result.TcpConnected);
        Assert.Contains("Demo or UAT", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("demo")]
    [InlineData("production-dry-run")]
    public void Non_authorized_activations_cannot_use_the_specialized_production_market_data_path(string activation)
    {
        var binding = ProductionBinding();
        var request = activation == "demo"
            ? Request(LmaxFixArch7bActivation.AuthorizedOnce)
            : WithProductionPacket(ProductionRequest(binding) with
            {
                Activation = LmaxFixArch7bActivation.ProductionDryRun,
                ProductionCommandConfirmed = false
            });
        var options = ProductionOptions(binding);
        options.AllowOrderSubmission = false;

        Assert.Contains("ARCH7B_PRODUCTION_READ_ONLY_MARKET_DATA_ACTIVATION_REQUIRED",
            LmaxFixArch7bKnownOrderContract.ValidateProductionReadOnlyMarketData(
                options, request, request.DeadlineUtc, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("order-submission", "ARCH7B_PRODUCTION_READ_ONLY_ORDER_SUBMISSION_FORBIDDEN")]
    [InlineData("live-trading", "ARCH7B_PRODUCTION_READ_ONLY_LIVE_TRADING_FORBIDDEN")]
    [InlineData("external-disabled", "ARCH7B_PRODUCTION_READ_ONLY_EXTERNAL_CONNECTIONS_REQUIRED")]
    [InlineData("dry-run", "ARCH7B_PRODUCTION_READ_ONLY_DRY_RUN_FORBIDDEN")]
    [InlineData("tls", "ARCH7B_PRODUCTION_FIX_TLS_REQUIRED")]
    [InlineData("md-host", "ARCH7B_PRODUCTION_MARKET_DATA_HOST_BINDING_MISMATCH")]
    [InlineData("md-port", "ARCH7B_PRODUCTION_MARKET_DATA_PORT_BINDING_MISMATCH")]
    [InlineData("md-target", "ARCH7B_PRODUCTION_MARKET_DATA_TARGET_BINDING_MISMATCH")]
    [InlineData("sender", "ARCH7B_PRODUCTION_FIX_SENDER_BINDING_MISMATCH")]
    [InlineData("instrument", "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH")]
    [InlineData("security", "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH")]
    [InlineData("mode", "ARCH7B_PRODUCTION_MARKET_DATA_REQUEST_MODE_INVALID")]
    [InlineData("encoding", "ARCH7B_PRODUCTION_MARKET_DATA_SYMBOL_ENCODING_INVALID")]
    [InlineData("depth", "ARCH7B_PRODUCTION_MARKET_DATA_DEPTH_INVALID")]
    [InlineData("wait", "ARCH7B_PRODUCTION_MARKET_DATA_WAIT_BUDGET_INVALID")]
    public void Specialized_production_market_data_validation_fails_closed_for_every_option_mismatch(
        string mutation,
        string expectedBlocker)
    {
        var binding = ProductionBinding();
        var request = ProductionRequest(binding);
        var options = ProductionOptions(binding);
        options.AllowOrderSubmission = false;
        switch (mutation)
        {
            case "order-submission": options.AllowOrderSubmission = true; break;
            case "live-trading": options.AllowLiveTrading = true; break;
            case "external-disabled": options.AllowExternalConnections = false; break;
            case "dry-run": options.DryRun = true; break;
            case "tls": options.UseTls = false; break;
            case "md-host": options.FixMarketDataHost = "other"; break;
            case "md-port": options.FixMarketDataPort = binding.FixMarketDataPort + 1; break;
            case "md-target": options.FixMarketDataTargetCompId = "other"; break;
            case "sender": options.FixSenderCompId = "other"; break;
            case "instrument": options.InstrumentSymbol = "OTHER"; break;
            case "security": options.LmaxInstrumentId = "OTHER"; break;
            case "mode": options.MarketDataRequestMode = LmaxFixMarketDataRequestMode.SnapshotOnly; break;
            case "encoding": options.MarketDataSymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.Auto; break;
            case "depth": options.MarketDepth = 2; break;
            case "wait": options.MarketDataMaxWaitSeconds = 6; break;
        }

        Assert.Contains(expectedBlocker, LmaxFixArch7bKnownOrderContract.ValidateProductionReadOnlyMarketData(
            options, request, request.DeadlineUtc, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("host")]
    [InlineData("port")]
    [InlineData("target")]
    public void Production_packet_binds_market_data_endpoint(string mutation)
    {
        var binding = ProductionBinding();
        var mutatedBinding = mutation switch
        {
            "host" => binding with { FixMarketDataHost = "other" },
            "port" => binding with { FixMarketDataPort = binding.FixMarketDataPort + 1 },
            "target" => binding with { FixMarketDataTargetCompId = "other" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var request = ProductionRequest(binding) with { ProductionBinding = mutatedBinding };
        var options = ProductionOptions(mutatedBinding);
        options.AllowOrderSubmission = false;

        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH",
            LmaxFixArch7bKnownOrderContract.ValidateProductionReadOnlyMarketData(
                options, request, request.DeadlineUtc, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Specialized_production_market_data_rejects_an_expired_lifecycle_deadline()
    {
        var binding = ProductionBinding();
        var request = WithProductionPacket(ProductionRequest(binding) with
        {
            DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        });
        var options = ProductionOptions(binding);
        options.AllowOrderSubmission = false;

        Assert.Contains("ARCH7B_PRODUCTION_MARKET_DATA_DEADLINE_EXCEEDED",
            LmaxFixArch7bKnownOrderContract.ValidateProductionReadOnlyMarketData(
                options, request, request.DeadlineUtc, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Fresh_production_runs_preflight_market_data_before_order_entry_and_opening_send()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "tools",
            "QQ.Production.Intraday.Lmax.ConnectivityLab",
            "RawFixSessionClient.Arch7b.cs"));
        var recoveryPlan = source.IndexOf("recoveryPlan = LmaxFixArch7bRecoveryPlanner.Build(recovery)", StringComparison.Ordinal);
        var preflight = source.IndexOf("ARCH7B_PRODUCTION_PREFLIGHT_MARKET_DATA_UNAVAILABLE", StringComparison.Ordinal);
        var revalidation = source.IndexOf("postPreflightBlockers", StringComparison.Ordinal);
        var orderEntry = source.IndexOf("orderEntryTcp = new TcpClient", StringComparison.Ordinal);
        var preOpeningRevalidation = source.IndexOf("preOpeningBlockers", StringComparison.Ordinal);
        var opening = source.IndexOf("var openingRequest", StringComparison.Ordinal);

        Assert.True(recoveryPlan >= 0 && recoveryPlan < preflight);
        Assert.True(preflight < revalidation && revalidation < orderEntry);
        Assert.True(orderEntry < preOpeningRevalidation && preOpeningRevalidation < opening);
        Assert.Contains("recoveryPlan.MaySendOpeningNewOrderSingle", source, StringComparison.Ordinal);
        Assert.Contains("request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Current_time_revalidation_rejects_a_stale_production_opening_packet_before_send()
    {
        var binding = ProductionBinding();
        var now = DateTimeOffset.UtcNow;
        var request = WithProductionPacket(ProductionRequest(binding) with
        {
            RegisteredAtUtc = now.AddSeconds(-10),
            BboAcquisitionStartedAtUtc = now.AddSeconds(-10),
            BboObservedAtUtc = now.AddSeconds(-6),
            OpeningCancelAtUtc = now.AddSeconds(10),
            DeadlineUtc = now.AddSeconds(60)
        });

        Assert.Contains("ARCH7B_BBO_STALE",
            LmaxFixArch7bKnownOrderContract.Validate(ProductionOptions(binding), request, now));
    }

    private static LmaxFixArch7bProductionBinding ProductionBinding()
        => new(
            "Production",
            "PROD-TEST-ACCOUNT",
            "fix.production.test",
            443,
            "LMXPRD",
            "PROD-SENDER",
            "md.production.test",
            444,
            "LMXMDPRD",
            "EURUSD",
            "9001",
            "8",
            0.2m,
            0.1m,
            0.00001m,
            2m,
            120,
            "prod-postgres.test",
            5432,
            "qq_pms_arch7b_production",
            "operator-test-production-once");

    private static LmaxFixArch7bKnownOrderRequest ProductionRequest(
        LmaxFixArch7bProductionBinding binding)
    {
        var now = DateTimeOffset.UtcNow;
        var request = Request(LmaxFixArch7bActivation.ProductionAuthorizedOnce) with
        {
            AccountId = binding.AccountId,
            OpeningLimitPrice = 1.10000m,
            MinimumOpeningPrice = 1.09980m,
            MaximumOpeningPrice = 1.10020m,
            BboBid = 1.09990m,
            BboAsk = 1.10000m,
            BboObservedAtUtc = now,
            RegisteredAtUtc = now,
            OpeningCancelAtUtc = now.AddSeconds(30),
            DeadlineUtc = now.AddSeconds(120),
            ProductionBinding = binding,
            ProductionCommandConfirmed = true,
            BboSymbol = binding.InstrumentSymbol,
            BboSecurityId = binding.SecurityId,
            BboAcquisitionStartedAtUtc = now
        };
        return request with
        {
            AuthorizationPacketSha256 =
                LmaxFixArch7bKnownOrderContract.ComputeProductionAuthorizationPacketSha256(request, binding)
        };
    }

    private static LmaxFixArch7bKnownOrderRequest WithProductionPacket(
        LmaxFixArch7bKnownOrderRequest request)
        => request with
        {
            AuthorizationPacketSha256 = LmaxFixArch7bKnownOrderContract
                .ComputeProductionAuthorizationPacketSha256(request, request.ProductionBinding!)
        };

    private static LmaxFixArch7bProductionReadinessBinding ReadinessBinding(
        LmaxFixArch7bProductionBinding binding)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            binding.EnvironmentName,
            binding.AccountId,
            binding.FixOrderHost,
            binding.FixOrderPort,
            binding.FixOrderTargetCompId,
            binding.FixSenderCompId,
            binding.FixMarketDataHost,
            binding.FixMarketDataPort,
            binding.FixMarketDataTargetCompId,
            binding.InstrumentSymbol,
            binding.SecurityId,
            binding.SecurityIdSource,
            binding.PersistenceHost,
            binding.PersistencePort,
            binding.PersistenceDatabase,
            binding.OperatorAuthorizationId,
            now,
            now.AddSeconds(120));
    }

    private static LmaxConnectivityLabOptions ProductionOptions(
        LmaxFixArch7bProductionBinding binding)
        => new()
        {
            EnvironmentName = binding.EnvironmentName,
            AccountCode = binding.AccountId,
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false,
            UseTls = true,
            FixOrderHost = binding.FixOrderHost,
            FixOrderPort = binding.FixOrderPort,
            FixOrderTargetCompId = binding.FixOrderTargetCompId,
            FixSenderCompId = "PROD-SENDER",
            FixMarketDataHost = binding.FixMarketDataHost,
            FixMarketDataPort = binding.FixMarketDataPort,
            FixMarketDataTargetCompId = binding.FixMarketDataTargetCompId,
            FixUsername = "synthetic-user",
            FixPassword = "synthetic-password",
            InstrumentSymbol = binding.InstrumentSymbol,
            LmaxInstrumentId = binding.SecurityId,
            FixSecurityIdSource = binding.SecurityIdSource,
            MarketDepth = 1,
            MarketDataMaxWaitSeconds = 5,
            MarketDataRequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            MarketDataSymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol
        };

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
            CompleteTopOfBook = true,
            ObservationCompletedAtUtc = completedAtUtc,
            Cleanup = new LmaxFixMarketDataCleanupSnapshot(
                unsubscribeAttempted: true,
                unsubscribeSent: true,
                unsubscribeMdReqId: "REQ-1",
                logoutAttempted: true,
                logoutSent: true,
                streamDisposeAttempted: true,
                streamDisposeSucceeded: true,
                socketDisposeAttempted: true,
                socketDisposeSucceeded: true)
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

    [Fact]
    public void ProductionSecretPrintConfigWrapper_IsProcessScoped_AndPrintConfigOnly()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "scripts",
            "lmax-production-print-config-from-aws-secret.ps1"));

        Assert.Contains("secretsmanager", source, StringComparison.Ordinal);
        Assert.Contains("get-secret-value", source, StringComparison.Ordinal);
        Assert.Contains("[Environment]::SetEnvironmentVariable", source, StringComparison.Ordinal);
        Assert.Contains("\"Process\"", source, StringComparison.Ordinal);
        Assert.Contains("--no-build --no-restore -- print-config", source, StringComparison.Ordinal);
        Assert.Contains("QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS\"] = \"false\"", source, StringComparison.Ordinal);
        Assert.Contains("QQ_LMAX_ALLOW_ORDER_SUBMISSION\"] = \"false\"", source, StringComparison.Ordinal);
        Assert.Contains("QQ_LMAX_ALLOW_LIVE_TRADING\"] = \"false\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("fix-arch7b-production-readiness", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Out-File", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Add-Content", source, StringComparison.Ordinal);
    }

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
