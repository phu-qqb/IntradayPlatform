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
    public async Task Account_api_smoke_is_skipped_when_external_connections_are_disabled()
    {
        var client = new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator(), new FakeHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var options = CompleteAccountOptions();
        options.AllowExternalConnections = false;

        var result = await client.DiscoverAsync(options, "account-api-smoke", ["/account"], showResponseExcerpt: false, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Contains("AllowExternalConnections=false", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Account_api_safety_blocks_non_https_production_and_order_submission()
    {
        var validator = new LmaxConnectivityLabSafetyValidator();
        var http = CompleteAccountOptions();
        http.AccountApiBaseUrl = "http://account-api.london-demo.lmax.com";
        http.AllowExternalConnections = true;
        var production = CompleteAccountOptions();
        production.EnvironmentName = "Production";
        production.AllowExternalConnections = true;
        var orderSubmission = CompleteAccountOptions();
        orderSubmission.AllowExternalConnections = true;
        orderSubmission.AllowOrderSubmission = true;

        Assert.Contains("HTTPS", string.Join(" ", validator.ValidateForAccountApi(http)), StringComparison.Ordinal);
        Assert.Contains("Demo or UAT", string.Join(" ", validator.ValidateForAccountApi(production)), StringComparison.Ordinal);
        Assert.Contains("AllowOrderSubmission=false", string.Join(" ", validator.ValidateForAccountApi(orderSubmission)), StringComparison.Ordinal);
    }

    [Fact]
    public void Account_api_auth_headers_are_built_and_masked()
    {
        var options = CompleteAccountOptions();
        options.AccountApiKey = "api-secret";
        options.AccountApiBearerToken = "bearer-secret";

        using var basic = new HttpRequestMessage(HttpMethod.Get, "/");
        using var bearer = new HttpRequestMessage(HttpMethod.Get, "/");
        using var header = new HttpRequestMessage(HttpMethod.Get, "/");
        LmaxAccountApiClient.ApplyAuth(basic, options, LmaxAccountApiAuthMode.BasicAuth);
        LmaxAccountApiClient.ApplyAuth(bearer, options, LmaxAccountApiAuthMode.BearerApiKey);
        LmaxAccountApiClient.ApplyAuth(header, options, LmaxAccountApiAuthMode.HeaderApiKey);

        Assert.Equal("Basic", basic.Headers.Authorization?.Scheme);
        Assert.Equal("Bearer", bearer.Headers.Authorization?.Scheme);
        Assert.True(header.Headers.Contains("X-API-Key"));
        Assert.DoesNotContain("api-secret", LmaxAccountApiClient.BuildMaskedAuthSummary(options, LmaxAccountApiAuthMode.HeaderApiKey), StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", LmaxAccountApiClient.BuildMaskedAuthSummary(options, LmaxAccountApiAuthMode.BearerApiKey), StringComparison.Ordinal);
    }

    [Fact]
    public void Account_api_auto_mode_prefers_basic_then_api_key()
    {
        var basic = CompleteAccountOptions();
        basic.AccountApiKey = "key";
        var keyOnly = CompleteAccountOptions();
        keyOnly.AccountApiUsername = null;
        keyOnly.AccountApiPassword = null;
        keyOnly.AccountApiKey = "key";

        Assert.Equal(LmaxAccountApiAuthMode.BasicAuth, LmaxAccountApiClient.ResolveAuthModes(basic).First());
        Assert.Equal(LmaxAccountApiAuthMode.BearerApiKey, LmaxAccountApiClient.ResolveAuthModes(keyOnly).First());
    }

    [Fact]
    public async Task Account_api_discovery_treats_404_as_non_fatal_and_401_as_auth_issue()
    {
        var client404 = new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator(), new FakeHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{}") }));
        var client401 = new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator(), new FakeHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) { Content = new StringContent("{}") }));
        var options = CompleteAccountOptions();
        options.AllowExternalConnections = true;

        var notFound = await client404.DiscoverAsync(options, "account-api-discover", ["/missing"], showResponseExcerpt: true, CancellationToken.None);
        var unauthorized = await client401.DiscoverAsync(options, "account-api-discover", ["/account"], showResponseExcerpt: true, CancellationToken.None);

        Assert.Equal("Skipped", notFound.Status);
        Assert.Equal("NotFound", notFound.EndpointProbes.Single().Status);
        Assert.Equal("Failed", unauthorized.Status);
        Assert.Equal("Unauthorized", unauthorized.EndpointProbes.Single().Status);
    }

    [Fact]
    public async Task Account_api_reachable_json_response_reports_safe_shape_without_secrets()
    {
        var client = new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator(), new FakeHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"positions\":[{\"symbol\":\"EURUSD\"}],\"wallets\":[]}") }));
        var options = CompleteAccountOptions();
        options.AllowExternalConnections = true;
        options.AccountApiPassword = "do-not-print";

        var result = await client.DiscoverAsync(options, "account-api-discover", ["/account"], showResponseExcerpt: true, CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.Contains("positions", result.EndpointProbes.Single().TopLevelFields);
        Assert.DoesNotContain("do-not-print", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-print", string.Join("|", result.EndpointProbes.Select(x => x.Excerpt)), StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_capability_scanner_detects_supported_and_unsupported_messages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lmax-fix-dictionary-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, """
            <fix>
              <messages>
                <message name="OrderStatusRequest" msgtype="H"><field name="ClOrdID" required="Y" /></message>
                <message name="ExecutionReport" msgtype="8"><field name="ExecID" required="Y" /></message>
                <message name="TradeCaptureReportRequest" msgtype="AD"><group name="TrdCapDtGrp" required="Y" /></message>
                <message name="TradeCaptureReportRequestAck" msgtype="AQ"><field name="TradeRequestID" required="Y" /></message>
                <message name="TradeCaptureReport" msgtype="AE"><field name="ExecID" required="Y" /></message>
              </messages>
            </fix>
            """);
        try
        {
            var result = LmaxFixRecoveryCodec.ScanDictionary(path);

            Assert.Equal("Ok", result.Status);
            Assert.Contains(result.Capabilities, x => x.MessageName == "OrderStatusRequest" && x.MsgType == "H" && x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "ExecutionReport" && x.MsgType == "8" && x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "TradeCaptureReportRequest" && x.MsgType == "AD" && x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "TradeCaptureReportRequestAck" && x.MsgType == "AQ" && x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "TradeCaptureReport" && x.MsgType == "AE" && x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "OrderMassStatusRequest" && x.MsgType == "AF" && !x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "RequestForPositions" && x.MsgType == "AN" && !x.Supported);
            Assert.Contains(result.Capabilities, x => x.MessageName == "PositionReport" && x.MsgType == "AP" && !x.Supported);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Trade_capture_request_builder_emits_read_only_ad_request()
    {
        var options = LmaxFixTradeCaptureRequestOptions.From(
            new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            60,
            null,
            null,
            "ACC1",
            10,
            50,
            false);

        var request = LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest("SENDER", "LMXBD", 2, "TCREQ1", options);

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}35=AD{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}568=TCREQ1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}569=1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}263=0{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}1=ACC1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}580=2{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Equal(2, LmaxFixMarketDataCodec.ParseFields(request).Count(x => x.Tag == "60"));
    }

    [Fact]
    public void Trade_capture_request_id_generator_stays_within_lmax_limit()
    {
        var id = LmaxFixRecoveryCodec.GenerateTradeRequestId(new DateTimeOffset(2026, 5, 5, 16, 39, 51, TimeSpan.Zero), 1);

        Assert.Equal("TC26050516395101", id);
        Assert.True(id.Length <= 16);
        Assert.All(id, ch => Assert.InRange(ch, (char)0, (char)127));
        Assert.DoesNotContain(id, char.IsWhiteSpace);
    }

    [Fact]
    public void Trade_capture_request_builder_rejects_long_trade_request_id_locally()
    {
        var options = LmaxFixTradeCaptureRequestOptions.From(DateTimeOffset.UtcNow, 60, null, null, null, 10, 50, false);

        var ex = Assert.Throws<ArgumentException>(() => LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest("SENDER", "LMXBD", 2, "QQTC-20260505163951923", options));

        Assert.Contains("16 characters or fewer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Trade_capture_request_builder_omits_account_when_absent()
    {
        var options = LmaxFixTradeCaptureRequestOptions.From(DateTimeOffset.UtcNow, 60, null, null, null, 10, 50, false);

        var request = LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest("SENDER", "LMXBD", 2, "TCREQ1", options);

        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}1=", request, StringComparison.Ordinal);
    }

    [Fact]
    public void Trade_capture_ack_parser_handles_accepted_and_rejected_acks()
    {
        var accepted = FixMessage("35=AQ", "568=TCREQ1", "569=1", "263=0", "748=0", "749=0", "750=0");
        var rejected = FixMessage("35=AQ", "568=TCREQ2", "749=1", "750=2", "58=Rejected window");

        var acceptedAck = LmaxFixRecoveryCodec.ParseTradeCaptureAck(accepted);
        var rejectedAck = LmaxFixRecoveryCodec.ParseTradeCaptureAck(rejected);

        Assert.True(acceptedAck.IsAck);
        Assert.True(acceptedAck.Accepted);
        Assert.Equal("TCREQ1", acceptedAck.RequestId);
        Assert.Equal("1", acceptedAck.TradeRequestType);
        Assert.Equal("0", acceptedAck.SubscriptionRequestType);
        Assert.Equal(0, acceptedAck.TotNumTradeReports);
        Assert.Equal("0", acceptedAck.Result);
        Assert.Equal("0", acceptedAck.Status);
        Assert.True(rejectedAck.IsAck);
        Assert.False(rejectedAck.Accepted);
        Assert.Equal("Rejected window", rejectedAck.Text);
    }

    [Fact]
    public void Trade_capture_ack_parser_handles_expected_report_count_and_missing_count()
    {
        var withReports = FixMessage("35=AQ", "568=TCREQ1", "748=2", "749=0", "750=0");
        var missingCount = FixMessage("35=AQ", "568=TCREQ2", "749=0", "750=0");

        var withReportsAck = LmaxFixRecoveryCodec.ParseTradeCaptureAck(withReports);
        var missingCountAck = LmaxFixRecoveryCodec.ParseTradeCaptureAck(missingCount);

        Assert.True(withReportsAck.Accepted);
        Assert.Equal(2, withReportsAck.TotNumTradeReports);
        Assert.True(missingCountAck.Accepted);
        Assert.Null(missingCountAck.TotNumTradeReports);
    }

    [Fact]
    public void Trade_capture_zero_report_ack_result_is_success_without_timeout()
    {
        var result = new LmaxFixTradeCaptureSmokeResult(
            "fix-trade-capture-smoke",
            "Ok",
            Connected: true,
            LoggedOn: true,
            RequestSent: true,
            AckReceived: true,
            AckAccepted: true,
            RequestRejected: false,
            AckRejectText: null,
            RejectMsgType: null,
            RejectRefTagId: null,
            RejectRefMsgType: null,
            RejectReasonCode: null,
            RejectText: null,
            LastReceivedMsgType: "AQ",
            ExpectedTradeReportCount: 0,
            NoMoreReports: true,
            LogoutSent: true,
            TradeReportCount: 0,
            LastReportRequested: true,
            Reports: [],
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Message: "Trade capture request accepted; no trade reports returned for the requested window.",
            SafetyDecisions: [],
            Diagnostics: []);

        Assert.Equal("Ok", result.Status);
        Assert.True(result.AckAccepted);
        Assert.Equal(0, result.ExpectedTradeReportCount);
        Assert.True(result.NoMoreReports);
        Assert.True(result.LogoutSent);
        Assert.DoesNotContain("timeout", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Trade_capture_expected_reports_timeout_result_is_partial_and_clear()
    {
        var result = new LmaxFixTradeCaptureSmokeResult(
            "fix-trade-capture-smoke",
            "PartiallySucceeded",
            Connected: true,
            LoggedOn: true,
            RequestSent: true,
            AckReceived: true,
            AckAccepted: true,
            RequestRejected: false,
            AckRejectText: null,
            RejectMsgType: null,
            RejectRefTagId: null,
            RejectRefMsgType: null,
            RejectReasonCode: null,
            RejectText: null,
            LastReceivedMsgType: "AE",
            ExpectedTradeReportCount: 2,
            NoMoreReports: false,
            LogoutSent: true,
            TradeReportCount: 1,
            LastReportRequested: false,
            Reports: [],
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Message: "Trade capture request accepted, but timed out after receiving 1 of 2 expected trade reports.",
            SafetyDecisions: [],
            Diagnostics: []);

        Assert.Equal("PartiallySucceeded", result.Status);
        Assert.Equal(2, result.ExpectedTradeReportCount);
        Assert.Contains("1 of 2", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_reject_parser_extracts_trade_capture_reject_fields()
    {
        var message = FixMessage("35=3", "45=2", "371=568", "372=AD", "373=6", "58=Trade request id max length 16");

        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);

        Assert.True(reject.IsReject);
        Assert.Equal("2", reject.RefSeqNum);
        Assert.Equal("568", reject.RefTagId);
        Assert.Equal("AD", reject.RefMsgType);
        Assert.Equal("6", reject.SessionRejectReason);
        Assert.Equal("Trade request id max length 16", reject.Text);
    }

    [Fact]
    public void Trade_capture_result_can_report_session_reject_without_timeout_message()
    {
        var result = new LmaxFixTradeCaptureSmokeResult(
            "fix-trade-capture-smoke",
            "Failed",
            Connected: true,
            LoggedOn: true,
            RequestSent: true,
            AckReceived: false,
            AckAccepted: false,
            RequestRejected: true,
            AckRejectText: "Trade request id max length 16",
            RejectMsgType: "3",
            RejectRefTagId: "568",
            RejectRefMsgType: "AD",
            RejectReasonCode: "6",
            RejectText: "Trade request id max length 16",
            LastReceivedMsgType: "3",
            ExpectedTradeReportCount: null,
            NoMoreReports: false,
            LogoutSent: true,
            TradeReportCount: 0,
            LastReportRequested: false,
            Reports: [],
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Message: "TradeCaptureReportRequest was rejected by FIX session reject: Trade request id max length 16",
            SafetyDecisions: [],
            Diagnostics: []);

        Assert.True(result.RequestRejected);
        Assert.Equal("3", result.LastReceivedMsgType);
        Assert.Equal("568", result.RejectRefTagId);
        Assert.DoesNotContain("timeout", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Trade_capture_report_parser_extracts_report_fields()
    {
        var message = FixMessage("35=AE", "568=TCREQ1", "571=TRPT1", "17=EXEC1", "527=SECEXEC1", "48=4001", "22=8", "55=EUR/USD", "32=1000", "31=1.17361", "75=20260505", "60=20260505-10:15:30.123", "54=1", "1=ACC1", "11=CL1", "37=ORD1", "912=Y");

        var report = LmaxFixRecoveryCodec.ParseTradeCaptureReport(message);

        Assert.Equal("EXEC1", report.ExecId);
        Assert.Equal("SECEXEC1", report.SecondaryExecId);
        Assert.Equal("4001", report.SecurityId);
        Assert.Equal("TRPT1", report.TradeReportId);
        Assert.Equal("EUR/USD", report.Symbol);
        Assert.Equal(1000m, report.LastQty);
        Assert.Equal(1.17361m, report.LastPx);
        Assert.Equal("20260505", report.TradeDate);
        Assert.Equal(new DateTimeOffset(2026, 5, 5, 10, 15, 30, 123, TimeSpan.Zero), report.TransactTime);
        Assert.Equal("1", report.Side);
        Assert.Equal(LmaxFixTradeCaptureSide.Buy, report.NormalizedSide);
        Assert.True(report.LastReportRequested);
        Assert.Equal("ACC1", report.Account);
        Assert.Equal("CL1", report.ClOrdId);
        Assert.Equal("ORD1", report.OrderId);
    }

    [Fact]
    public void Trade_capture_normalization_maps_security_id_4001_to_eurusd()
    {
        var message = FixMessage("35=AE", "568=TCREQ1", "17=EXEC1", "48=4001", "22=8", "32=1000", "31=1.17361", "75=20260505", "60=20260505-10:15:30.123", "54=2", "1=ACC1");

        var normalized = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, CompleteFixOptions());

        Assert.Equal("EURUSD", normalized.Report.InternalSymbol);
        Assert.Equal(LmaxFixTradeCaptureSide.Sell, normalized.Report.NormalizedSide);
        Assert.Equal(-1000m, normalized.EodShape.UnitsBoughtSold);
        Assert.Equal(1173.61000m, normalized.EodShape.NotionalValue);
    }

    [Fact]
    public void Trade_capture_normalization_handles_symbol_without_security_id()
    {
        var message = FixMessage("35=AE", "568=TCREQ1", "17=EXEC1", "55=EUR/USD", "32=1000", "31=1.17361", "75=20260505", "60=20260505-10:15:30.123", "54=1");

        var normalized = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, CompleteFixOptions());

        Assert.Equal("EURUSD", normalized.Report.InternalSymbol);
        Assert.True(normalized.Report.CanMapToEodIndividualTrade);
    }

    [Fact]
    public void Trade_capture_normalization_warns_for_missing_optional_and_malformed_fields()
    {
        var message = FixMessage("35=AE", "568=TCREQ1", "17=EXEC1", "48=4001", "32=not-a-decimal", "31=bad-price", "60=bad-time", "54=9");

        var normalized = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, CompleteFixOptions());

        Assert.Contains(normalized.Warnings, x => x.Contains("LastQty", StringComparison.Ordinal));
        Assert.Contains(normalized.Warnings, x => x.Contains("LastPx", StringComparison.Ordinal));
        Assert.Contains(normalized.Warnings, x => x.Contains("TransactTime", StringComparison.Ordinal));
        Assert.Contains(normalized.Warnings, x => x.Contains("Side", StringComparison.Ordinal));
        Assert.Contains(normalized.MissingForEodComparison, x => x.Contains("ClOrdID", StringComparison.Ordinal));
        Assert.Contains(normalized.MissingForEodComparison, x => x.Contains("TradeUTI", StringComparison.Ordinal));
    }

    [Fact]
    public void Trade_capture_to_eod_shape_mapper_projects_comparison_fields()
    {
        var message = FixMessage("35=AE", "568=TCREQ1", "17=EXEC1", "527=MTF1", "48=4001", "22=8", "55=EUR/USD", "32=1000", "31=1.17361", "75=20260505", "60=20260505-10:15:30.123", "54=1", "1=ACC1", "11=CL1", "37=ORD1");

        var mapped = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, CompleteFixOptions()).EodShape;

        Assert.Equal("EXEC1", mapped.ExecutionId);
        Assert.Equal("MTF1", mapped.MtfExecutionId);
        Assert.Equal(1000m, mapped.TradeQuantity);
        Assert.Equal(1.17361m, mapped.TradePrice);
        Assert.Equal("4001", mapped.InstrumentId);
        Assert.Equal("EURUSD", mapped.Symbol);
        Assert.Equal("CL1", mapped.InstructionId);
        Assert.Equal("ORD1", mapped.OrderId);
        Assert.Equal("ACC1", mapped.AccountId);
    }

    [Fact]
    public async Task Trade_capture_replay_command_is_local_only()
    {
        var runner = CreateRunner();
        using var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var fixture = Path.Combine(FindRepoRoot(), "tools", "QQ.Production.Intraday.Lmax.ConnectivityLab", "fixtures", "synthetic-trade-capture-ae.fix");
            var exitCode = await runner.RunAsync(["fix-trade-capture-replay", $"--fixture={fixture}"], CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains("No network call was made", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Synthetic AE replay completed", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Order_status_request_builder_emits_read_only_h_request()
    {
        var request = LmaxFixRecoveryCodec.BuildOrderStatusRequest("SENDER", "LMXBD", 2, "CL1", account: "ACC1", securityId: "4001", securityIdSource: "8", side: "1", ordStatusReqId: "OSR1");

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}35=H{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}11=CL1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}790=OSR1{LmaxFixMarketDataCodec.Soh}", request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trade_capture_smoke_skips_without_external_connections_and_masks_password()
    {
        var fix = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var options = CompleteFixOptions();
        options.FixPassword = "trade-capture-secret-password";
        var request = LmaxFixTradeCaptureRequestOptions.From(DateTimeOffset.UtcNow, 60, null, null, null, 10, 50, false);

        var result = await fix.TradeCaptureSmokeAsync(options, request, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.DoesNotContain("trade-capture-secret-password", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("trade-capture-secret-password", string.Join("|", result.Diagnostics), StringComparison.Ordinal);
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
        => new(new PlaceholderLmaxPublicDataClient(), new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator()), new PlaceholderLmaxFixSessionClient(), new LmaxConnectivityLabSafetyValidator());

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

    private static LmaxConnectivityLabOptions CompleteAccountOptions()
        => new()
        {
            EnvironmentName = "Demo",
            AccountApiBaseUrl = "https://account-api.london-demo.lmax.com",
            AccountApiAuthMode = LmaxAccountApiAuthMode.Auto,
            AccountApiUsername = "demo-user",
            AccountApiPassword = "demo-password",
            AllowExternalConnections = false,
            AllowOrderSubmission = false,
            AllowLiveTrading = false
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

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
