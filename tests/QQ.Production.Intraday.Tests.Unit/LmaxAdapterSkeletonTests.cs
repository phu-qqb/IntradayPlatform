using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxAdapterSkeletonTests
{
    [Fact]
    public void Lmax_adapter_options_default_disabled()
    {
        var options = new LmaxFixAdapterOptions();

        Assert.False(options.Enabled);
        Assert.False(options.ShadowModeEnabled);
        Assert.False(options.AllowExternalConnections);
        Assert.False(options.AllowOrderSubmission);
        Assert.False(options.AllowLiveTrading);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Runtime_safety_validator_rejects_live_trading_order_submission_and_production()
    {
        var validator = new LmaxFixAdapterRuntimeSafetyValidator();
        var live = validator.Validate(new LmaxFixAdapterOptions { EnvironmentName = "Demo", AllowLiveTrading = true }, LmaxFixAdapterRuntimeIntent.ShadowOnly);
        var order = validator.Validate(new LmaxFixAdapterOptions { EnvironmentName = "Demo", AllowOrderSubmission = true, DryRun = false, OrderHost = "fix-order.london-demo.lmax.com" }, LmaxFixAdapterRuntimeIntent.OrderSubmission);
        var production = validator.Validate(new LmaxFixAdapterOptions { EnvironmentName = "Production" }, LmaxFixAdapterRuntimeIntent.ShadowOnly);

        Assert.Contains(live.Decisions, x => x.Gate == "AllowLiveTrading" && !x.Passed);
        Assert.Contains(order.Decisions, x => x.Gate == "RuntimeOrderSubmission" && !x.Passed);
        Assert.Contains(order.Decisions, x => x.Gate == "AllowOrderSubmission" && !x.Passed);
        Assert.Contains(production.Decisions, x => x.Gate == "Production" && !x.Passed);
    }

    [Fact]
    public async Task Venue_gateway_skeleton_blocks_submit_without_network_or_order_submission()
    {
        var skeleton = new LmaxVenueGatewaySkeleton();

        var result = await skeleton.SubmitOrderAsync(NewOrderRequest());

        Assert.False(result.Submitted);
        Assert.True(result.Blocked);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void New_order_single_builder_omits_handl_inst_and_includes_proven_fields()
    {
        var message = new LmaxFixOrderMessageBuilder().BuildNewOrderSingle(NewOrderRequest());

        Assert.Equal("D", message.MessageType);
        Assert.Equal("D", message.Tags["35"]);
        Assert.Equal("DL26050607454402", message.Tags["11"]);
        Assert.Equal("4001", message.Tags["48"]);
        Assert.Equal("8", message.Tags["22"]);
        Assert.Equal("EURUSD", message.Tags["55"]);
        Assert.Equal("1", message.Tags["54"]);
        Assert.Equal("0.1", message.Tags["38"]);
        Assert.Equal("1", message.Tags["40"]);
        Assert.Equal("3", message.Tags["59"]);
        Assert.False(message.Tags.ContainsKey("21"));
    }

    [Fact]
    public void Limit_new_order_single_includes_price()
    {
        var request = NewOrderRequest() with { OrderType = "Limit", LimitPrice = 1.17m };

        var message = new LmaxFixOrderMessageBuilder().BuildNewOrderSingle(request);

        Assert.Equal("2", message.Tags["40"]);
        Assert.Equal("1.17", message.Tags["44"]);
    }

    [Fact]
    public void Trade_request_id_and_trade_capture_builder_use_lmax_shape()
    {
        var now = new DateTimeOffset(2026, 5, 6, 8, 24, 19, TimeSpan.Zero);
        var id = LmaxFixOrderMessageBuilder.GenerateTradeRequestId(now, 2);

        var message = new LmaxFixOrderMessageBuilder().BuildTradeCaptureReportRequest(new LmaxFixTradeCaptureRequest(id, now.AddMinutes(-5), now.AddMinutes(1), null));

        Assert.True(id.Length <= 16);
        Assert.Equal("AD", message.Tags["35"]);
        Assert.Equal(id, message.Tags["568"]);
        Assert.Equal("1", message.Tags["569"]);
        Assert.Equal("0", message.Tags["263"]);
        Assert.Equal("2", message.Tags["580"]);
        Assert.Equal(2, LmaxFixTagParser.GetValues(message.SanitizedMessage, "60").Count);
        Assert.Empty(message.Warnings);
    }

    [Fact]
    public void Trade_capture_builder_warns_on_oversized_trade_request_id()
    {
        var message = new LmaxFixOrderMessageBuilder().BuildTradeCaptureReportRequest(new LmaxFixTradeCaptureRequest("TOO-LONG-TRADE-REQUEST-ID", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, null));

        Assert.Contains(message.Warnings, x => x.Contains("568", StringComparison.Ordinal));
    }

    [Fact]
    public void Order_status_builder_emits_read_only_order_status_request()
    {
        var message = new LmaxFixOrderMessageBuilder().BuildOrderStatusRequest(new LmaxFixOrderStatusRequest("DL26050607454402", "4001", null, VenueAdapterContractSide.Buy, null, null));

        Assert.Equal("H", message.Tags["35"]);
        Assert.Equal("DL26050607454402", message.Tags["11"]);
        Assert.Equal("4001", message.Tags["48"]);
        Assert.Equal("8", message.Tags["22"]);
        Assert.Equal("1", message.Tags["54"]);
    }

    [Fact]
    public void Market_data_builder_uses_security_id_and_bid_offer_entry_types()
    {
        var message = new LmaxFixOrderMessageBuilder().BuildMarketDataRequest(new LmaxFixMarketDataRequest("MD1", "4001", "8", "EURUSD", 1, true));

        Assert.Equal("V", message.Tags["35"]);
        Assert.Equal("4001", message.Tags["48"]);
        Assert.Equal("8", message.Tags["22"]);
        Assert.Contains("0", LmaxFixTagParser.GetValues(message.SanitizedMessage, "269"));
        Assert.Contains("1", LmaxFixTagParser.GetValues(message.SanitizedMessage, "269"));
    }

    [Fact]
    public void Skeleton_mappers_preserve_contract_responsibilities()
    {
        var executionMapper = new LmaxFixExecutionEventMapper();
        var statusMapper = new LmaxFixOrderStatusMapper();
        var tradeCaptureMapper = new LmaxFixTradeCaptureMapper();
        var rejectMapper = new LmaxFixRejectMapper();

        var fill = executionMapper.Map(ExecutionReport(LmaxNormalizedExecutionType.Trade, LmaxNormalizedOrderStatusValue.Filled, leavesQty: 0m));
        var orderStatus = statusMapper.Map(ExecutionReport(LmaxNormalizedExecutionType.OrderStatus, LmaxNormalizedOrderStatusValue.Filled, leavesQty: 0m));
        var tradeCapture = tradeCaptureMapper.Map(TradeCaptureReport());
        var reject = rejectMapper.Map(new LmaxNormalizedFixReject("2", "21", "D", "0", "UnknownTag", null));

        Assert.Equal(VenueExecutionEventType.Fill, fill.EventType);
        Assert.Equal(VenueExecutionEventType.OrderStatus, orderStatus.EventType);
        Assert.Equal(VenueExecutionEventType.TradeCaptureRecovery, tradeCapture.EventType);
        Assert.True(tradeCapture.IsRecoveryEvidence);
        Assert.Equal(VenueExecutionEventType.ProtocolReject, reject.EventType);
    }

    [Fact]
    public void Missing_trade_uti_remains_warning_only()
    {
        var report = TradeCaptureReport();

        Assert.Contains("TradeUTI", report.MissingForEodComparison);
        Assert.Contains(report.Warnings, x => x.Contains("TradeUTI", StringComparison.Ordinal));
    }

    [Fact]
    public void Market_data_mapper_maps_bid_offer_mid()
    {
        var snapshot = new LmaxFixMarketDataMapper().Map(
            "4001",
            "EURUSD",
            [
                new LmaxFixMarketDataEntry("0", 1.1739m, 1m, new DateTimeOffset(2026, 5, 6, 8, 0, 0, TimeSpan.Zero)),
                new LmaxFixMarketDataEntry("1", 1.1741m, 1m, new DateTimeOffset(2026, 5, 6, 8, 0, 0, TimeSpan.Zero))
            ]);

        Assert.Equal(1.1739m, snapshot.Bid);
        Assert.Equal(1.1741m, snapshot.Ask);
        Assert.Equal(1.1740m, snapshot.Mid);
        Assert.Equal("EURUSD", snapshot.InternalSymbol);
    }

    [Fact]
    public void No_password_or_secret_appears_in_diagnostics()
    {
        var message = new LmaxFixOrderMessageBuilder().BuildNewOrderSingle(NewOrderRequest() with { Account = "account-secret-token" });

        Assert.DoesNotContain("account-secret-token", message.SanitizedMessage, StringComparison.Ordinal);
        Assert.Contains("********", message.SanitizedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_do_not_register_lmax_skeleton_or_runtime_adapter()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxFixAdapterOptions", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxFixAdapterOptions", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxFixOrderMessageBuilder", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxFixOrderMessageBuilder", workerProgram, StringComparison.Ordinal);
    }

    private static LmaxFixNewOrderSingleRequest NewOrderRequest()
        => new(
            "DL26050607454402",
            "4001",
            "8",
            "EURUSD",
            VenueAdapterContractSide.Buy,
            0.1m,
            "Market",
            "IOC",
            null,
            null,
            new DateTimeOffset(2026, 5, 6, 7, 45, 44, TimeSpan.Zero));

    private static LmaxNormalizedExecutionReport ExecutionReport(LmaxNormalizedExecutionType execType, LmaxNormalizedOrderStatusValue status, decimal leavesQty)
        => new(
            "EXEC-1",
            "ORDER-1",
            "DL26050607454402",
            null,
            execType,
            execType == LmaxNormalizedExecutionType.Trade ? "F" : execType == LmaxNormalizedExecutionType.OrderStatus ? "I" : "0",
            status,
            status == LmaxNormalizedOrderStatusValue.Filled ? "2" : "0",
            "4001",
            "8",
            "EURUSD",
            "EURUSD",
            LmaxNormalizedSide.Buy,
            0.1m,
            leavesQty,
            0.1m - leavesQty,
            execType == LmaxNormalizedExecutionType.Trade ? 0.1m - leavesQty : null,
            execType == LmaxNormalizedExecutionType.Trade ? 1.17394m : null,
            1.17394m,
            null,
            new DateTimeOffset(2026, 5, 6, 8, 32, 32, TimeSpan.Zero),
            null,
            null,
            execType == LmaxNormalizedExecutionType.Trade,
            null,
            new DateTimeOffset(2026, 5, 6, 8, 32, 32, TimeSpan.Zero),
            []);

    private static LmaxNormalizedTradeCaptureReport TradeCaptureReport()
        => new LmaxFixTradeCaptureNormalizer().Normalize(new Dictionary<string, string>
        {
            ["35"] = "AE",
            ["568"] = "TC260506083201",
            ["17"] = "EXEC-1",
            ["527"] = "MTF-1",
            ["37"] = "ORDER-1",
            ["11"] = "DL26050607454402",
            ["48"] = "4001",
            ["22"] = "8",
            ["55"] = "EURUSD",
            ["54"] = "1",
            ["32"] = "0.1",
            ["31"] = "1.17394",
            ["75"] = "20260506",
            ["60"] = "20260506-08:32:32.677",
            ["912"] = "Y"
        });

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
