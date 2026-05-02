using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxConnectivityLabTests
{
    [Fact]
    public void Default_config_blocks_external_calls_and_order_submission()
    {
        var options = new LmaxConnectivityLabOptions();
        var validator = new LmaxConnectivityLabSafetyValidator();

        Assert.Contains(validator.ValidateForExternalCall(options), x => x.Contains("AllowExternalConnections=false", StringComparison.Ordinal));
        Assert.Contains(validator.ValidateForOrderSubmission(options, explicitConfirmation: false), x => x.Contains("AllowOrderSubmission=false", StringComparison.Ordinal));
    }

    [Fact]
    public void AllowLiveTrading_is_rejected()
    {
        var options = new LmaxConnectivityLabOptions { AllowExternalConnections = true, AllowLiveTrading = true };

        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForExternalCall(options);

        Assert.Contains(issues, x => x.Contains("AllowLiveTrading=true", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_environment_order_submission_is_rejected()
    {
        var options = new LmaxConnectivityLabOptions
        {
            EnvironmentName = "Production",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false
        };

        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForOrderSubmission(options, explicitConfirmation: true);

        Assert.Contains(issues, x => x.Contains("Production", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_or_uat_dry_run_command_is_allowed_without_network()
    {
        var runner = CreateRunner();
        var result = runner.OrderLifecycleDryRun(new LmaxConnectivityLabOptions { EnvironmentName = "Demo" });

        Assert.Equal("Ok", result.Status);
        Assert.Contains(result.SafetyDecisions, x => x.Contains("No order was submitted", StringComparison.Ordinal));
    }

    [Fact]
    public void Order_lifecycle_demo_requires_explicit_confirmation()
    {
        var runner = CreateRunner();
        var options = new LmaxConnectivityLabOptions
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false
        };

        var result = runner.OrderLifecycleDemo(options, explicitConfirmation: false);

        Assert.Equal("Blocked", result.Status);
        Assert.Contains("explicit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Print_config_masks_secrets()
    {
        var options = new LmaxConnectivityLabOptions
        {
            AccountApiKey = "secret-api-key",
            FixUsername = "fix-user",
            FixSenderCompId = "sender",
            FixPassword = "fix-password"
        };

        var safe = options.ToSafeDictionary();

        Assert.Equal("********", safe["AccountApiKey"]);
        Assert.Equal("********", safe["FixUsername"]);
        Assert.Equal("********", safe["FixSenderCompId"]);
        Assert.Equal("********", safe["FixPassword"]);
        var safeValues = string.Join("|", safe.Values);
        Assert.DoesNotContain("secret-api-key", safeValues, StringComparison.Ordinal);
        Assert.DoesNotContain("fix-password", safeValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commands_skip_without_config_or_external_permission()
    {
        var publicData = new PlaceholderLmaxPublicDataClient();
        var account = new PlaceholderLmaxAccountClient();
        var fix = new PlaceholderLmaxFixSessionClient();
        var options = new LmaxConnectivityLabOptions();

        var publicResult = await publicData.SmokeAsync(options, CancellationToken.None);
        var accountResult = await account.SmokeAsync(new LmaxConnectivityLabOptions { AllowExternalConnections = true }, CancellationToken.None);
        var fixResult = fix.Validate(options, marketData: false);

        Assert.Equal("Skipped", publicResult.Status);
        Assert.Equal("Skipped", accountResult.Status);
        Assert.Equal("Skipped", fixResult.Status);
    }

    [Fact]
    public async Task Fix_logon_smoke_is_skipped_when_external_connections_are_disabled()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();

        var result = await fix.LogonSmokeAsync(options, marketData: false, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.False(result.Connected);
        Assert.False(result.LoggedOn);
        Assert.Contains("AllowExternalConnections=false", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_logon_smoke_is_skipped_when_password_is_missing()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();
        options.AllowExternalConnections = true;
        options.FixPassword = null;

        var result = await fix.LogonSmokeAsync(options, marketData: false, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Contains("Missing FixPassword", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_logon_smoke_is_skipped_when_host_or_port_is_missing()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();
        options.AllowExternalConnections = true;
        options.FixOrderHost = null;
        options.FixOrderPort = null;

        var result = await fix.LogonSmokeAsync(options, marketData: false, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Contains("Missing FixOrderHost", result.Message, StringComparison.Ordinal);
        Assert.Contains("Missing FixOrderPort", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_logon_safety_blocks_local_live_trading_and_order_submission()
    {
        var validator = new LmaxConnectivityLabSafetyValidator();
        var local = CompleteFixOptions();
        local.EnvironmentName = "Local";
        local.AllowExternalConnections = true;

        var live = CompleteFixOptions(liveTrading: true);
        live.AllowExternalConnections = true;

        var orderSubmission = CompleteFixOptions(orderSubmission: true);
        orderSubmission.AllowExternalConnections = true;

        Assert.Contains("Demo or UAT", string.Join(" ", validator.ValidateForFixLogon(local, marketData: false)), StringComparison.Ordinal);
        Assert.Contains("AllowLiveTrading=true", string.Join(" ", validator.ValidateForFixLogon(live, marketData: false)), StringComparison.Ordinal);
        Assert.Contains("AllowOrderSubmission=false", string.Join(" ", validator.ValidateForFixLogon(orderSubmission, marketData: false)), StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_external_fix_config_can_reach_attempt_gate_without_network_in_unit_test()
    {
        var options = CompleteFixOptions();
        options.AllowExternalConnections = true;

        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForFixLogon(options, marketData: false);

        Assert.Empty(issues);
    }

    [Fact]
    public void Market_data_request_builder_creates_snapshot_request()
    {
        var request = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", CompleteMarketDataRequestOptions());

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}35=V{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}262=REQ1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}263=0{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}264=1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}269=0{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}269=1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}48=4001{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}55=EUR/USD{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_request_builder_creates_update_and_unsubscribe_requests()
    {
        var updateOptions = CompleteMarketDataRequestOptions() with { RequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates };

        var subscribe = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", updateOptions);
        var unsubscribe = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 3, "REQ1", updateOptions, unsubscribe: true);

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}263=1{LmaxFixMarketDataCodec.Soh}", subscribe, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}263=2{LmaxFixMarketDataCodec.Soh}", unsubscribe, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_request_builder_supports_no_id_source_variants()
    {
        var securityAndSymbol = CompleteMarketDataRequestOptions() with { SymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource };
        var securityOnly = CompleteMarketDataRequestOptions() with { SymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SecurityIdNoIdSource };
        var slash = CompleteMarketDataRequestOptions() with { SymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SlashSymbol };

        var securityAndSymbolMessage = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", securityAndSymbol);
        var securityOnlyMessage = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", securityOnly);
        var slashMessage = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", slash);

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}48=4001{LmaxFixMarketDataCodec.Soh}", securityAndSymbolMessage, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}55=EUR/USD{LmaxFixMarketDataCodec.Soh}", securityAndSymbolMessage, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}22=8{LmaxFixMarketDataCodec.Soh}", securityAndSymbolMessage, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}48=4001{LmaxFixMarketDataCodec.Soh}", securityOnlyMessage, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}22=8{LmaxFixMarketDataCodec.Soh}", securityOnlyMessage, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}55=EUR/USD{LmaxFixMarketDataCodec.Soh}", securityOnlyMessage, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}55=EUR/USD{LmaxFixMarketDataCodec.Soh}", slashMessage, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}48=4001{LmaxFixMarketDataCodec.Soh}", slashMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_request_builder_security_id_mode_matches_validated_demo_path()
    {
        var options = CompleteMarketDataRequestOptions() with { SymbolEncodingMode = LmaxFixMarketDataSymbolEncodingMode.SecurityId };

        var request = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", options);

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}35=V{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}48=4001{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}22=8{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}55=EUR/USD{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_request_diagnostics_describe_groups()
    {
        var request = LmaxFixMarketDataCodec.BuildMarketDataRequest("SENDER", "LMXBDM", 2, "REQ1", CompleteMarketDataRequestOptions());

        var diagnostics = string.Join("|", LmaxFixMarketDataCodec.DescribeMarketDataRequest(request));

        Assert.Contains("MDReqID=REQ1", diagnostics, StringComparison.Ordinal);
        Assert.Contains("SubscriptionRequestType=0", diagnostics, StringComparison.Ordinal);
        Assert.Contains("MdEntryTypeCount=2", diagnostics, StringComparison.Ordinal);
        Assert.Contains("MdEntryTypesSent=0,1", diagnostics, StringComparison.Ordinal);
        Assert.Contains("RelatedSymCount=1", diagnostics, StringComparison.Ordinal);
        Assert.Contains("InstrumentFieldsSent=48=4001,22=8,55=EUR/USD", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_reject_parser_extracts_reason_and_text()
    {
        var message = FixMessage("35=Y", "262=REQ1", "281=2", "58=Unknown symbol");

        var reject = LmaxFixMarketDataCodec.ParseReject(message);

        Assert.True(reject.IsReject);
        Assert.Equal("REQ1", reject.MdReqId);
        Assert.Equal("2", reject.Reason);
        Assert.Equal("Unknown symbol", reject.Text);
    }

    [Fact]
    public void Market_data_snapshot_parser_extracts_bid_ask_and_mid_as_decimal()
    {
        var message = FixMessage("35=W", "262=REQ1", "55=EUR/USD", "48=4001", "268=2", "269=0", "270=1.10000", "271=1000000", "269=1", "270=1.10020", "271=1000000");

        var entries = LmaxFixMarketDataCodec.ParseMarketDataEntries(message);
        var (bid, ask, mid) = LmaxFixMarketDataCodec.ComputeTopOfBook(entries);

        Assert.Equal(2, entries.Count);
        Assert.Equal(1.10000m, bid);
        Assert.Equal(1.10020m, ask);
        Assert.Equal(1.10010m, mid);
    }

    [Fact]
    public void Market_data_snapshot_parser_matches_validated_lmax_demo_security_id_shape()
    {
        var message = FixMessage("35=W", "262=QQMD-VALIDATED", "48=4001", "268=2", "269=0", "270=1.17361", "271=50", "269=1", "270=1.17368", "271=200");

        var entries = LmaxFixMarketDataCodec.ParseMarketDataEntries(message);
        var (bid, ask, mid) = LmaxFixMarketDataCodec.ComputeTopOfBook(entries);

        Assert.Equal("4001", entries[0].SecurityId);
        Assert.Contains(entries, x => x.EntryType == "0" && x.Price == 1.17361m && x.Size == 50m);
        Assert.Contains(entries, x => x.EntryType == "1" && x.Price == 1.17368m && x.Size == 200m);
        Assert.Equal(1.17361m, bid);
        Assert.Equal(1.17368m, ask);
        Assert.Equal(1.173645m, mid);
    }

    [Fact]
    public void Market_data_incremental_parser_extracts_bid_and_ask_entries()
    {
        var message = FixMessage("35=X", "262=REQ1", "268=2", "279=0", "269=0", "270=1.09990", "271=500000", "279=0", "269=1", "270=1.10010", "271=600000");

        var entries = LmaxFixMarketDataCodec.ParseMarketDataEntries(message);

        Assert.Contains(entries, x => x.MessageType == LmaxFixMarketDataMessageType.IncrementalRefresh && x.EntryType == "0" && x.Price == 1.09990m);
        Assert.Contains(entries, x => x.MessageType == LmaxFixMarketDataMessageType.IncrementalRefresh && x.EntryType == "1" && x.Price == 1.10010m);
    }

    [Fact]
    public async Task Market_data_snapshot_smoke_is_skipped_when_gates_fail()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();

        var result = await fix.MarketDataSnapshotSmokeAsync(options, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.False(result.RequestSent);
        Assert.False(result.TcpConnected);
        Assert.False(result.TlsHandshakeCompleted);
        Assert.False(result.FixLogonSent);
        Assert.False(result.FixLoggedOn);
        Assert.Contains("AllowExternalConnections=false", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_snapshot_result_preserves_phase_state_for_post_logon_timeout()
    {
        var result = LmaxFixMarketDataSmokeResult.Create(
            "Failed",
            "Market data snapshot smoke timed out during market data response.",
            DateTimeOffset.UtcNow,
            tcpConnected: true,
            tlsHandshakeCompleted: true,
            fixLogonSent: true,
            fixLoggedOn: true,
            marketDataRequestSent: true,
            marketDataSnapshotReceived: false,
            marketDataRejectReceived: false,
            logoutSent: false,
            rejectReason: null,
            rejectText: null,
            lastReceivedMsgType: "A",
            safetyDecisions: [],
            diagnostics: [],
            attempts: ["Encoding=SecurityIdAndSymbol: started"]);

        Assert.True(result.TcpConnected);
        Assert.True(result.TlsHandshakeCompleted);
        Assert.True(result.FixLoggedOn);
        Assert.True(result.MarketDataRequestSent);
        Assert.False(result.MarketDataSnapshotReceived);
    }

    [Fact]
    public async Task Market_data_snapshot_diagnostics_do_not_include_password()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();
        options.FixPassword = "super-secret-password";

        var result = await fix.MarketDataSnapshotSmokeAsync(options, CancellationToken.None);

        Assert.DoesNotContain("super-secret-password", string.Join("|", result.Diagnostics), StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Market_data_snapshot_uses_market_data_fix_config_in_diagnostics()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();

        var result = await fix.MarketDataSnapshotSmokeAsync(options, CancellationToken.None);

        var diagnostics = string.Join("|", result.Diagnostics);
        Assert.Contains("Host=fix-marketdata.london-demo.lmax.com", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Port=443", diagnostics, StringComparison.Ordinal);
        Assert.Contains("TargetCompId=LMXBDM", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("fix-order.london-demo.lmax.com", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetCompId=LMXBD|", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_snapshot_command_arguments_preserve_external_flag_and_timeouts()
    {
        var options = LmaxConnectivityLabOptions.FromEnvironmentAndArgs([
            "--allow-external-connections=true",
            "--instrument=EURUSD",
            "--lmax-instrument-id=4001",
            "--slash-symbol=EUR/USD",
            "--market-depth=1",
            "--request-mode=Auto",
            "--symbol-encoding-mode=Auto",
            "--show-fix-messages=true",
            "--connect-timeout-seconds=11",
            "--logon-timeout-seconds=12",
            "--max-wait-seconds=13",
            "--max-messages=7"
        ]);

        Assert.True(options.AllowExternalConnections);
        Assert.Equal("EURUSD", options.InstrumentSymbol);
        Assert.Equal("4001", options.LmaxInstrumentId);
        Assert.Equal("EUR/USD", options.LmaxSlashSymbol);
        Assert.Equal(LmaxFixMarketDataSymbolEncodingMode.Auto, options.MarketDataSymbolEncodingMode);
        Assert.Equal(LmaxFixMarketDataRequestMode.Auto, options.MarketDataRequestMode);
        Assert.True(options.ShowFixMessages);
        Assert.Equal(11, options.ConnectTimeoutSeconds);
        Assert.Equal(12, options.LogonTimeoutSeconds);
        Assert.Equal(13, options.MarketDataMaxWaitSeconds);
        Assert.Equal(7, options.MarketDataMaxMessages);
    }

    [Fact]
    public void Market_data_snapshot_safety_blocks_order_submission_and_live_trading()
    {
        var validator = new LmaxConnectivityLabSafetyValidator();
        var live = CompleteFixOptions(liveTrading: true);
        live.AllowExternalConnections = true;
        var orderSubmission = CompleteFixOptions(orderSubmission: true);
        orderSubmission.AllowExternalConnections = true;

        Assert.Contains("AllowLiveTrading=true", string.Join(" ", validator.ValidateForFixLogon(live, marketData: true)), StringComparison.Ordinal);
        Assert.Contains("AllowOrderSubmission=false", string.Join(" ", validator.ValidateForFixLogon(orderSubmission, marketData: true)), StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_do_not_reference_connectivity_lab()
    {
        var root = FindRepoRoot();
        var apiProject = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "QQ.Production.Intraday.Api.csproj"));
        var workerProject = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "QQ.Production.Intraday.Worker.csproj"));

        Assert.DoesNotContain("ConnectivityLab", apiProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectivityLab", workerProject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_and_worker_still_register_fake_lmax_gateway_only()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", workerProgram, StringComparison.Ordinal);
    }

    private static LmaxConnectivityLabRunner CreateRunner()
        => new(new PlaceholderLmaxPublicDataClient(), new PlaceholderLmaxAccountClient(), new PlaceholderLmaxFixSessionClient(), new LmaxConnectivityLabSafetyValidator());

    private static LmaxConnectivityLabOptions CompleteFixOptions(bool liveTrading = false, bool orderSubmission = false)
        => new()
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = false,
            AllowLiveTrading = liveTrading,
            AllowOrderSubmission = orderSubmission,
            DryRun = true,
            FixOrderHost = "fix-order.london-demo.lmax.com",
            FixOrderPort = 443,
            FixOrderTargetCompId = "LMXBD",
            FixMarketDataHost = "fix-marketdata.london-demo.lmax.com",
            FixMarketDataPort = 443,
            FixMarketDataTargetCompId = "LMXBDM",
            FixSenderCompId = "sender",
            FixUsername = "username",
            FixPassword = "password",
            LmaxSlashSymbol = "EUR/USD",
            FixSecurityIdSource = "8"
        };

    private static LmaxFixMarketDataRequestOptions CompleteMarketDataRequestOptions()
        => new(
            "EURUSD",
            "4001",
            "EUR/USD",
            1,
            LmaxFixMarketDataRequestMode.SnapshotOnly,
            10,
            5,
            LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol,
            "8",
            false);

    private static string FixMessage(params string[] fields)
        => string.Join(LmaxFixMarketDataCodec.Soh, fields) + LmaxFixMarketDataCodec.Soh;

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
