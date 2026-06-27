extern alias M2C1A;
extern alias M2C1ATool;

using System.Reflection;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using M2C1A::QQ.Production.Intraday.Infrastructure.Lmax;
using M2C1A::QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;
using M2C1A::QQ.Production.Intraday.Lmax.ConnectivityLab;
using M2C1A::QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;
using M2C1ATool::QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class M2C1ALmaxMarketDataOnlyTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("V")]
    public void T01_whitelist_allows_only_readonly_session_and_market_data_messages(string msgType)
    {
        Assert.True(LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType(msgType).Allowed);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("F")]
    [InlineData("G")]
    [InlineData("H")]
    [InlineData("AE")]
    [InlineData("ZZ")]
    public void T02_whitelist_rejects_order_recovery_account_or_unknown_messages_fail_closed(string msgType)
    {
        var result = LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType(msgType);
        Assert.False(result.Allowed);
        Assert.True(result.SessionMustStop);
        Assert.Equal(LmaxMarketDataOnlyGuardStatus.FailClosed, result.Status);
    }

    [Fact]
    public void T03_existing_connectivity_lab_parser_is_source_linked_and_parses_bbo()
    {
        var message = Fix("35=W", "34=10", "52=20260625-08:00:00.001", "262=REQ1", "55=EUR/USD", "48=4001", "268=2", "269=0", "270=1.10000", "271=1000000", "269=1", "270=1.10020", "271=2000000");
        var entries = LmaxFixMarketDataCodec.ParseMarketDataEntries(message);
        var top = LmaxFixMarketDataCodec.ComputeTopOfBook(entries);
        Assert.Equal(1.10000m, top.BestBid);
        Assert.Equal(1.10020m, top.BestAsk);
        Assert.Equal(1.10010m, top.Mid);
    }

    [Fact]
    public void T04_observation_mapper_emits_v2_with_socket_receive_monotonic_parser_and_valid_book()
    {
        var mapper = new LmaxMarketDataOnlyObservationMapper(Instruments);
        var obs = mapper.Map(Frame(Fix("35=W", "34=1", "52=20260625-08:00:00.001", "55=EUR/USD", "48=4001", "268=2", "269=0", "270=1.10000", "271=100", "269=1", "270=1.10020", "271=200")), new HashSet<string>(["EURUSD"], StringComparer.OrdinalIgnoreCase));
        Assert.True(obs.BookValid);
        Assert.Equal("35=W", obs.SourceMessageType);
        Assert.Equal("EURUSD", obs.Symbol);
        Assert.Equal("4001", obs.InstrumentId);
        Assert.Equal(1234, obs.LocalMonotonicTimestamp);
        Assert.Equal(LmaxMarketDataOnlyObservationMapper.ParserVersion, obs.ParserVersion);
    }

    [Theory]
    [InlineData("35=W|34=2|52=20260625-08:00:00.001|55=EUR/USD|48=4001|268=1|269=1|270=1.10020|271=200")]
    [InlineData("35=W|34=3|52=20260625-08:00:00.001|55=EUR/USD|48=4001|268=2|269=0|270=1.10030|271=100|269=1|270=1.10020|271=200")]
    [InlineData("35=W|34=4|52=20260625-08:00:00.001|55=NOPE|48=9999|268=2|269=0|270=1.1|271=100|269=1|270=1.2|271=100")]
    public void T05_book_validation_rejects_invalid_books(string pipeFix)
    {
        var mapper = new LmaxMarketDataOnlyObservationMapper(Instruments);
        var obs = mapper.Map(Frame(PipeToSoh(pipeFix)), new HashSet<string>(["EURUSD"], StringComparer.OrdinalIgnoreCase));
        Assert.False(obs.BookValid);
    }

    [Fact]
    public async Task T06_fake_source_implements_ireadonlymarketdatasource_without_network_or_order_methods()
    {
        var source = new LmaxMarketDataOnlyFakeSource(Instruments, [Frame(Fix("35=W", "34=1", "52=20260625-08:00:00.001", "55=EUR/USD", "48=4001", "268=2", "269=0", "270=1.10000", "271=100", "269=1", "270=1.10020", "271=200"))]);
        var methods = source.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToArray();
        Assert.Contains("StartAsync", methods);
        Assert.DoesNotContain(methods, x => x.Contains("Order", StringComparison.OrdinalIgnoreCase) || x.Contains("Cancel", StringComparison.OrdinalIgnoreCase) || x.Contains("Account", StringComparison.OrdinalIgnoreCase));
        await source.StartAsync();
        await source.SubscribeAsync([new ReadOnlyMarketDataSubscription("4001", "EURUSD")]);
        var rows = new List<ReadOnlyMarketDataObservationV1>();
        await foreach (var row in source.ReadMarketDataAsync()) rows.Add(row);
        Assert.Single(rows);
        Assert.True(rows[0].BookValid);
        Assert.False(source.Health.State is ReadOnlyMarketDataFeedState.Failed);
    }

    [Fact]
    public void T07_feed_does_not_resynchronize_after_gap_without_explicit_full_refresh_all_symbols()
    {
        var machine = new ReadOnlyMarketDataFeedStateMachine();
        machine.OnStart();
        machine.OnConnected();
        machine.OnSubscribing([new ReadOnlyMarketDataSubscription("4001", "EURUSD"), new ReadOnlyMarketDataSubscription("4007", "AUDUSD")]);
        machine.OnSynchronized();
        machine.OnGap();
        machine.OnSynchronized();
        Assert.Equal(ReadOnlyMarketDataFeedState.Recovering, machine.State);
        machine.OnFullRefreshSynchronized(new HashSet<string>(["EURUSD"], StringComparer.OrdinalIgnoreCase));
        Assert.Equal(ReadOnlyMarketDataFeedState.Recovering, machine.State);
        machine.OnFullRefreshSynchronized(new HashSet<string>(["EURUSD", "AUDUSD"], StringComparer.OrdinalIgnoreCase));
        Assert.Equal(ReadOnlyMarketDataFeedState.Synchronized, machine.State);
    }

    [Fact]
    public void T08_market_data_only_project_has_no_order_capable_project_reference()
    {
        var projectText = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly", "QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly.csproj"));
        Assert.DoesNotContain("Infrastructure.Lmax\\QQ.Production.Intraday.Infrastructure.Lmax.csproj", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlServer", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LabMarketData.cs", projectText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T09_reflection_scan_finds_no_active_order_account_or_db_public_methods_on_market_data_only_types()
    {
        var assembly = typeof(LmaxMarketDataOnlyPreflight).Assembly;
        var forbidden = assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => $"{type.FullName}.{method.Name}"))
            .Where(name => name.Contains("NewOrderSingle", StringComparison.OrdinalIgnoreCase) || name.Contains("OrderCancelRequest", StringComparison.OrdinalIgnoreCase) || name.Contains("CancelOrder", StringComparison.OrdinalIgnoreCase) || name.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase) || name.Contains("DbConnection", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(forbidden);
    }

    [Fact]
    public void T10_preflight_rejects_non_demo_order_shapes_wrong_scope_and_unknown_outbound_fix()
    {
        var config = ValidConfig() with { Environment = "LIVE", CredentialScope = "ORDER_ENTRY", AllowedOutboundFixMsgTypes = ["V", "D"] };
        var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: false, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false);
        Assert.Equal("NO_GO_M2C1B", report.Status);
        Assert.Contains("first_capture_environment_must_be_DEMO", report.Issues);
        Assert.Contains("credential_scope_must_be_MARKET_DATA_ONLY", report.Issues);
        Assert.Contains("outbound_fix_whitelist_contains_forbidden_or_unknown_type", report.Issues);
        Assert.Contains("order_entry_disabled_flag_required", report.Issues);
    }

    [Fact]
    public async Task T11_preflight_cli_writes_m2c1b_reports_without_network()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-preflight", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "config.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") }), CanonicalRecorderV2Constants.JsonOptions));
        var output = Path.Combine(root, "out");
        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["preflight", "--config", configPath, "--output", output, "--network-disabled", "--no-order-entry", "--no-account-api", "--no-db"]);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(output, "m2c1b_preflight_report.json")));
        Assert.True(File.Exists(Path.Combine(output, "m2c1b_dependency_gate.json")));
        Assert.True(File.Exists(Path.Combine(output, "m2c1b_fix_whitelist_report.json")));
        Assert.True(File.Exists(Path.Combine(output, "m2c1b_binary_fingerprint.json")));
        var command = await File.ReadAllTextAsync(Path.Combine(output, "m2c1b_operator_command.txt"));
        Assert.Contains("capture --config", command, StringComparison.Ordinal);
        Assert.Contains("--operator-approved-market-data-fix-logon", command, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T12_recorder_accepts_capture_only_events_and_replay_has_exact_counts()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-recorder", Guid.NewGuid().ToString("N"));
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), 1_000_000);
        await using var recorder = await CanonicalRecorderV2.CreateAsync(new CanonicalRecorderV2Options(root, "m2c1b-run", "DEMO", "test", "git", "c1769aa", "cfg", ["LMAX_MARKET_DATA_CAPTURE_ONLY"], ["EURUSD"], [], [], [], FlushInterval: TimeSpan.FromMilliseconds(1)), clock);
        Assert.True(await recorder.RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_SESSION_STATE", "LMAX_MARKET_DATA_CAPTURE_ONLY", "SessionState", "v1", new { state = "connected" })));
        Assert.True(await recorder.RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_SUBSCRIPTION_STATE", "LMAX_MARKET_DATA_CAPTURE_ONLY", "SubscriptionState", "v1", new { symbol = "EURUSD", state = "subscribed" }, InstrumentId: "4001", Symbol: "EURUSD")));
        Assert.True(await recorder.RecordAsync(new CanonicalRecorderV2Event("BBO_UPDATED", "LMAX_MARKET_DATA_CAPTURE_ONLY", "ReadOnlyMarketDataObservationV2", "v2", new { bid = 1.1m, ask = 1.2m }, InstrumentId: "4001", Symbol: "EURUSD", Venue: "LMAX_DEMO_READ_ONLY", BookValid: true)));
        await recorder.CompleteAsync();
        var replay = await new CanonicalRecorderV2Replayer().ReplaySnapshotAsync(recorder.RunRoot);
        Assert.Equal("PASS", replay.ReplayReport.Status);
        Assert.Equal(3, replay.Events.Count);
        Assert.DoesNotContain(replay.Events, x => x.EventType.Contains("TARGET", StringComparison.OrdinalIgnoreCase) || x.EventType.Contains("INTENT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T13_real_lmax_readonly_transport_is_source_linked_into_market_data_only_assembly()
    {
        var assembly = typeof(LmaxMarketDataOnlyPreflight).Assembly;
        Assert.Same(assembly, typeof(LmaxRealReadOnlyMarketDataTransport).Assembly);
        Assert.Same(assembly, typeof(LmaxExecutableReadOnlyMarketDataSessionClient).Assembly);
        Assert.Same(assembly, typeof(LmaxReadOnlyActivationManualMarketDataRequestOperation).Assembly);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), x => string.Equals(x.Name, "QQ.Production.Intraday.Infrastructure.Lmax", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("D")]
    [InlineData("F")]
    [InlineData("G")]
    [InlineData("H")]
    [InlineData("q")]
    [InlineData("AD")]
    [InlineData("AE")]
    [InlineData("ZZ")]
    public async Task T14_guarded_write_stream_blocks_forbidden_fix_messages_before_fake_socket_writer(string msgType)
    {
        using var fakeSocket = new MemoryStream();
        await using var guarded = new LmaxMarketDataOnlyGuardedWriteStream(fakeSocket);
        var frame = Encoding.ASCII.GetBytes(FixFrame(msgType));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await guarded.WriteAsync(frame));

        Assert.Contains("market_data_only_forbidden_outbound_fix_msg_type", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, fakeSocket.Length);
        Assert.Equal(1, guarded.ForbiddenOutboundCount);
        Assert.False(guarded.Events.Single().Allowed);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("V")]
    public async Task T15_guarded_write_stream_allows_only_market_data_session_whitelist_to_fake_socket(string msgType)
    {
        using var fakeSocket = new MemoryStream();
        await using var guarded = new LmaxMarketDataOnlyGuardedWriteStream(fakeSocket);
        var frame = Encoding.ASCII.GetBytes(FixFrame(msgType));

        await guarded.WriteAsync(frame);

        Assert.True(fakeSocket.Length > 0);
        Assert.Equal(0, guarded.ForbiddenOutboundCount);
        Assert.True(guarded.Events.Single().Allowed);
        Assert.Equal(msgType, guarded.Events.Single().MsgType);
    }

    [Fact]
    public void T16_real_runtime_factory_creates_transport_without_order_entry_project_reference()
    {
        var transport = LmaxMarketDataOnlyRealRuntimeFactory.CreateTransport(allowCredentialMaterial: false);
        var assemblyNames = typeof(LmaxMarketDataOnlyRealRuntimeFactory).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.IsType<LmaxRealReadOnlyMarketDataTransport>(transport);
        Assert.DoesNotContain("QQ.Production.Intraday.Infrastructure.Lmax", assemblyNames);
        Assert.DoesNotContain(assemblyNames, x => x is not null && x.Contains("SqlServer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T17_catalog_resolves_eurusd_from_existing_connectivity_lab_config()
    {
        var catalog = LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConnectivityLab(FindRepoRoot());
        var eurusd = catalog.ResolveApproved("EURUSD");
        Assert.Equal("EURUSD", eurusd.Symbol);
        Assert.Equal("4001", eurusd.SecurityId);
        Assert.Equal("8", eurusd.SecurityIdSource);
        Assert.Equal("EUR/USD", eurusd.LmaxSlashSymbol);
        Assert.Equal("M2C1B_EXPLICIT_DEMO_MARKET_DATA_ONLY_SCOPE", eurusd.PermissionScope);
        Assert.Throws<InvalidOperationException>(() => catalog.ResolveApproved("GBPUSD"));
    }

    [Fact]
    public void T18_market_data_request_is_parameterized_for_approved_eurusd_security_id()
    {
        var catalog = LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConnectivityLab(FindRepoRoot());
        var frame = LmaxMarketDataOnlyCaptureRunner.BuildMarketDataRequest(ValidConfig(), catalog.ResolveApproved("EURUSD"), "SENDER", "TARGET");
        Assert.Equal("V", LmaxFixMarketDataCodec.GetMsgType(frame));
        Assert.Equal("1", LmaxFixMarketDataCodec.GetTag(frame, "263"));
        Assert.Equal("1", LmaxFixMarketDataCodec.GetTag(frame, "264"));
        Assert.Equal("2", LmaxFixMarketDataCodec.GetTag(frame, "267"));
        Assert.Equal("4001", LmaxFixMarketDataCodec.GetTag(frame, "48"));
        Assert.Equal("8", LmaxFixMarketDataCodec.GetTag(frame, "22"));
        Assert.DoesNotContain("4002", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("GBP/USD", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void T19_inbound_fix_frame_buffer_handles_split_and_concatenated_frames()
    {
        var first = Encoding.ASCII.GetBytes(MarketDataFrame(1, 1.10000m, 1.10020m));
        var second = Encoding.ASCII.GetBytes(MarketDataFrame(2, 1.10001m, 1.10021m));
        var buffer = new LmaxMarketDataOnlyFixFrameBuffer();
        var firstHalf = first.AsSpan(0, first.Length / 2).ToArray();
        var secondHalfAndNext = first.AsSpan(first.Length / 2).ToArray().Concat(second).ToArray();

        var incomplete = buffer.Append(firstHalf, DateTimeOffset.UtcNow, 101);
        var complete = buffer.Append(secondHalfAndNext, DateTimeOffset.UtcNow, 102);

        Assert.Empty(incomplete.Frames);
        Assert.False(incomplete.Malformed);
        Assert.Equal(2, complete.Frames.Count);
        Assert.Equal(1, complete.Frames[0].LocalEventOrder);
        Assert.Equal(2, complete.Frames[1].LocalEventOrder);
        Assert.Equal("W", LmaxFixMarketDataCodec.GetMsgType(complete.Frames[0].RawFixMessage));
    }

    [Fact]
    public async Task T20_guarded_write_buffers_split_allowed_frame_and_writes_after_completion()
    {
        using var fakeSocket = new MemoryStream();
        await using var guarded = new LmaxMarketDataOnlyGuardedWriteStream(fakeSocket);
        var frame = Encoding.ASCII.GetBytes(FixFrame("V"));
        await guarded.WriteAsync(frame.AsMemory(0, frame.Length / 2));
        Assert.Equal(0, fakeSocket.Length);
        Assert.Empty(guarded.Events);

        await guarded.WriteAsync(frame.AsMemory(frame.Length / 2));

        Assert.Equal(frame.Length, fakeSocket.Length);
        Assert.Single(guarded.Events);
        Assert.True(guarded.Events[0].Allowed);
    }

    [Fact]
    public async Task T21_guarded_write_validates_multiple_allowed_frames_in_one_write()
    {
        using var fakeSocket = new MemoryStream();
        await using var guarded = new LmaxMarketDataOnlyGuardedWriteStream(fakeSocket);
        var frames = Encoding.ASCII.GetBytes(FixFrame("0") + FixFrame("V", 2));

        await guarded.WriteAsync(frames);

        Assert.Equal(frames.Length, fakeSocket.Length);
        Assert.Equal(2, guarded.Events.Count);
        Assert.All(guarded.Events, e => Assert.True(e.Allowed));
    }

    [Fact]
    public async Task T22_guarded_write_blocks_allowed_plus_forbidden_composite_buffer_before_any_socket_write()
    {
        using var fakeSocket = new MemoryStream();
        await using var guarded = new LmaxMarketDataOnlyGuardedWriteStream(fakeSocket);
        var frames = Encoding.ASCII.GetBytes(FixFrame("0") + FixFrame("D", 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await guarded.WriteAsync(frames));

        Assert.Contains("market_data_only_forbidden_outbound_fix_msg_type:D", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, fakeSocket.Length);
        Assert.Equal(1, guarded.ForbiddenOutboundCount);
        Assert.Equal(2, guarded.Events.Count);
        Assert.True(guarded.Events[0].Allowed);
        Assert.False(guarded.Events[1].Allowed);
    }

    [Fact]
    public async Task T23_synthetic_capture_records_multiple_bbos_and_zero_target_decision_intent_events()
    {
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(Path.GetTempPath(), "m2c1b-synthetic", Guid.NewGuid().ToString("N")) });
        var runner = new LmaxMarketDataOnlyCaptureRunner(LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConnectivityLab(FindRepoRoot()));

        var result = await runner.CaptureSyntheticAsync(config, [MarketDataFrame(1, 1.10000m, 1.10020m), MarketDataFrame(2, 1.10001m, 1.10021m)]);

        Assert.Equal("GO_M2C2_CAPTURE_VALIDATED", result.Status);
        Assert.Equal(2, result.MarketDataReceived);
        Assert.Equal(2, result.BboUpdated);
        Assert.Equal("PASS", result.ReplayStatus);
        Assert.Equal(0, result.WriterErrorCount);
        Assert.Equal(0, result.DroppedEventCount);
        var replay = await new CanonicalRecorderV2Replayer().ReplaySnapshotAsync(result.RunRoot);
        Assert.DoesNotContain(replay.Events, e => e.EventType.Contains("TARGET", StringComparison.OrdinalIgnoreCase) || e.EventType.Contains("DECISION", StringComparison.OrdinalIgnoreCase) || e.EventType.Contains("INTENT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task T24_synthetic_capture_aborts_on_inbound_execution_report_35_8()
    {
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(Path.GetTempPath(), "m2c1b-synthetic-abort", Guid.NewGuid().ToString("N")) });
        var runner = new LmaxMarketDataOnlyCaptureRunner(LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConnectivityLab(FindRepoRoot()));

        var result = await runner.CaptureSyntheticAsync(config, [MarketDataFrame(1, 1.10000m, 1.10020m), FixFrame("8", 2, [("11", "SHOULD_NOT_EXIST")])]);

        Assert.Equal("NO_GO_M2C1B", result.Status);
        Assert.True(result.InboundExecutionReportObserved);
        Assert.Equal("forbidden_inbound_execution_report_35_8", result.StopReason);
    }

    [Fact]
    public async Task T25_capture_command_requires_explicit_operator_fix_logon_flag()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-capture-flags", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "config.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") }), CanonicalRecorderV2Constants.JsonOptions));

        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["capture", "--config", configPath, "--no-order-entry", "--no-account-api", "--no-db"]);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task T26_capture_command_synthetic_replay_runs_final_preflight_and_writes_manifest_without_network()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-capture-replay", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") });
        var configPath = Path.Combine(root, "config.json");
        var replayPath = Path.Combine(root, "replay.fix");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, CanonicalRecorderV2Constants.JsonOptions));
        await WritePackagedCatalogAsync(root);
        await File.WriteAllLinesAsync(replayPath, [MarketDataFrame(1, 1.10000m, 1.10020m), MarketDataFrame(2, 1.10001m, 1.10021m)]);

        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["capture", "--config", configPath, "--operator-approved-market-data-fix-logon", "--no-order-entry", "--no-account-api", "--no-db", "--synthetic-replay", replayPath]);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(config.OutputRoot, "m2c1b_preflight_report.json")));
        Assert.True(File.Exists(Path.Combine(config.OutputRoot, "m2c1b_capture_command_result.json")));
        Assert.Contains(Directory.EnumerateFiles(config.OutputRoot, "m2c1b_capture_manifest.json", SearchOption.AllDirectories), p => File.ReadAllText(p).Contains("GO_M2C2_CAPTURE_VALIDATED", StringComparison.Ordinal));
    }

    [Fact]
    public void T27_preflight_rejects_invalid_bounds_before_any_network_capture()
    {
        var config = ValidConfig() with { MaxDurationSeconds = 0, MaxEvents = 0, MaxTotalBytes = 0, MinimumFreeDiskBytes = 0 };
        var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false);
        Assert.Equal("NO_GO_M2C1B", report.Status);
        Assert.Contains("max_duration_seconds_required", report.Issues);
        Assert.Contains("max_events_required", report.Issues);
        Assert.Contains("max_total_bytes_required", report.Issues);
        Assert.Contains("minimum_free_disk_bytes_required", report.Issues);
    }
    [Fact]
    public async Task T28_synthetic_capture_treats_configured_event_bound_as_successful_stop()
    {
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(Path.GetTempPath(), "m2c1b-synthetic-bound", Guid.NewGuid().ToString("N")), MaxEvents = 1 });
        var runner = new LmaxMarketDataOnlyCaptureRunner(LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConnectivityLab(FindRepoRoot()));

        var result = await runner.CaptureSyntheticAsync(config, [MarketDataFrame(1, 1.10000m, 1.10020m), MarketDataFrame(2, 1.10001m, 1.10021m)]);

        Assert.Equal("GO_M2C2_CAPTURE_VALIDATED", result.Status);
        Assert.Equal("max_events_reached", result.StopReason);
        Assert.Equal(1, result.MarketDataReceived);
        Assert.Equal(1, result.BboUpdated);
    }


    [Fact]
    public void T29_secrets_manager_arn_for_market_data_only_passes_structured_preflight()
    {
        var config = ValidConfig() with
        {
            MarketDataCredentialReference = "aws-secretsmanager:arn:aws:secretsmanager:eu-west-2:761018894194:secret:qq/fund-platform/demo/lmax/market-data"
        };
        using var document = JsonDocument.Parse(SerializeConfig(config));

        var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false, configDocument: document.RootElement);

        Assert.Equal("GO_M2C1B_PREFLIGHT_READY", report.Status);
        Assert.DoesNotContain("config_contains_order_account_or_db_shape", report.Issues);
        Assert.Empty(LmaxMarketDataOnlyPreflight.FindForbiddenConfigShapeIssues(config, document.RootElement));
    }

    [Fact]
    public void T30_structured_preflight_rejects_order_entry_credential_field()
    {
        var (config, document) = ConfigWithExtraTopLevelProperty("\"order_entry_credentials\":{\"credential_reference\":\"redacted\"}");
        using (document)
        {
            var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false, configDocument: document.RootElement);

            Assert.Equal("NO_GO_M2C1B", report.Status);
            Assert.Contains("config_contains_order_account_or_db_shape", report.Issues);
            Assert.Contains(LmaxMarketDataOnlyPreflight.FindForbiddenConfigShapeIssues(config, document.RootElement), x => x.Contains("order_entry_credentials", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void T31_structured_preflight_rejects_account_api_field()
    {
        var (config, document) = ConfigWithExtraTopLevelProperty("\"account_api\":{\"enabled\":true}");
        using (document)
        {
            var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false, configDocument: document.RootElement);

            Assert.Equal("NO_GO_M2C1B", report.Status);
            Assert.Contains("config_contains_order_account_or_db_shape", report.Issues);
        }
    }

    [Fact]
    public void T32_structured_preflight_rejects_db_connection_field()
    {
        var (config, document) = ConfigWithExtraTopLevelProperty("\"db_connection\":\"Server=blocked\"");
        using (document)
        {
            var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false, configDocument: document.RootElement);

            Assert.Equal("NO_GO_M2C1B", report.Status);
            Assert.Contains("config_contains_order_account_or_db_shape", report.Issues);
        }
    }

    [Fact]
    public void T33_structured_preflight_rejects_password_bearing_config_field()
    {
        var (config, document) = ConfigWithExtraTopLevelProperty("\"market_data_password\":\"RAW_SECRET_SENTINEL\"");
        using (document)
        {
            var report = LmaxMarketDataOnlyPreflight.Validate(config, networkDisabled: true, noOrderEntry: true, noAccountApi: true, noDb: true, outputRootMustBeEmpty: false, configDocument: document.RootElement);

            Assert.Equal("NO_GO_M2C1B", report.Status);
            Assert.Contains("config_contains_order_account_or_db_shape", report.Issues);
            Assert.Contains(LmaxMarketDataOnlyPreflight.FindForbiddenConfigShapeIssues(config, document.RootElement), x => x.Contains("market_data_password", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task T34_preflight_cli_does_not_print_or_persist_secret_values_from_forbidden_config_fields()
    {
        const string sentinel = "RAW_SECRET_SENTINEL_AWS2B";
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-secret-redaction", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") });
        var configPath = Path.Combine(root, "config.json");
        var output = Path.Combine(root, "out");
        await File.WriteAllTextAsync(configPath, AppendTopLevelProperty(SerializeConfig(config), $"\"password\":\"{sentinel}\""));

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        int exit;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["preflight", "--config", configPath, "--output", output, "--network-disabled", "--no-order-entry", "--no-account-api", "--no-db"]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Equal(1, exit);
        var emitted = stdout + stderr.ToString() + string.Concat(Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(sentinel, emitted, StringComparison.Ordinal);
        Assert.Contains("config_contains_order_account_or_db_shape", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T35_packaged_catalog_loads_eurusd_and_rejects_unknown_instrument()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-packaged-catalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await WritePackagedCatalogAsync(root);

        var catalog = LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConfigDirectory(root);
        var eurusd = catalog.ResolveApproved("EURUSD");

        Assert.Equal("EURUSD", eurusd.Symbol);
        Assert.Equal("4001", eurusd.SecurityId);
        Assert.Equal("8", eurusd.SecurityIdSource);
        Assert.Equal("EUR/USD", eurusd.LmaxSlashSymbol);
        Assert.Equal("M2C1B_EXPLICIT_DEMO_MARKET_DATA_ONLY_SCOPE", eurusd.PermissionScope);
        Assert.Throws<InvalidOperationException>(() => catalog.ResolveApproved("GBPUSD"));
    }

    [Fact]
    public async Task T36_capture_command_synthetic_replay_uses_packaged_catalog_without_repo_root_or_order_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-capture-packaged-catalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Assert.False(File.Exists(Path.Combine(root, "QQ.Production.Intraday.sln")));
        await WritePackagedCatalogAsync(root);
        var config = WithValidHash(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") });
        var configPath = Path.Combine(root, "config.json");
        var replayPath = Path.Combine(root, "replay.fix");
        await File.WriteAllTextAsync(configPath, SerializeConfig(config));
        await File.WriteAllLinesAsync(replayPath, [MarketDataFrame(1, 1.10000m, 1.10020m)]);

        var programText = File.ReadAllText(Path.Combine(FindRepoRoot(), "tools", "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly", "Program.cs"));
        Assert.DoesNotContain("FindRepoRoot", programText, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadFromConnectivityLab", programText, StringComparison.Ordinal);

        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["capture", "--config", configPath, "--operator-approved-market-data-fix-logon", "--no-order-entry", "--no-account-api", "--no-db", "--synthetic-replay", replayPath]);

        Assert.Equal(0, exit);
        var result = await File.ReadAllTextAsync(Path.Combine(config.OutputRoot, "m2c1b_capture_command_result.json"));
        Assert.Contains("GO_M2C2_CAPTURE_VALIDATED", result, StringComparison.Ordinal);
        Assert.DoesNotContain("order_entry", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account_api", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_connection", result, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task T37_capture_command_synthetic_replay_allows_existing_recorder_root_contents()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1b-existing-recorder-root", Guid.NewGuid().ToString("N"));
        var recorderRoot = Path.Combine(root, "recorder");
        Directory.CreateDirectory(recorderRoot);
        await File.WriteAllTextAsync(Path.Combine(recorderRoot, "existing_m2c1b_report.json"), "{}");
        await WritePackagedCatalogAsync(root);
        var config = WithValidHash(ValidConfig() with { OutputRoot = recorderRoot });
        var configPath = Path.Combine(root, "config.json");
        var replayPath = Path.Combine(root, "replay.fix");
        await File.WriteAllTextAsync(configPath, SerializeConfig(config));
        await File.WriteAllLinesAsync(replayPath, [MarketDataFrame(1, 1.10000m, 1.10020m)]);

        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["capture", "--config", configPath, "--operator-approved-market-data-fix-logon", "--no-order-entry", "--no-account-api", "--no-db", "--synthetic-replay", replayPath]);

        Assert.Equal(0, exit);
        var report = await File.ReadAllTextAsync(Path.Combine(recorderRoot, "m2c1b_preflight_report.json"));
        Assert.DoesNotContain("output_root_not_empty", report, StringComparison.Ordinal);
        Assert.Contains("GO_M2C1B_PREFLIGHT_READY", report, StringComparison.Ordinal);
    }
    private static IReadOnlyList<LmaxMarketDataOnlyInstrument> Instruments =>
    [
        new("4001", "EURUSD", "EUR/USD"),
        new("4007", "AUDUSD", "AUD/USD")
    ];

    private static LmaxMarketDataOnlyRawFixFrame Frame(string message)
        => new(message, new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), 1234);

    private static string Fix(params string[] fields)
        => PipeToSoh(string.Join('|', fields));

    private static string FixFrame(string msgType, int sequenceNumber = 1, IReadOnlyList<(string Tag, string Value)>? fields = null)
        => LmaxFixMarketDataCodec.BuildMessage(msgType, sequenceNumber, "SENDER", "TARGET", fields ?? []);

    private static string MarketDataFrame(int sequenceNumber, decimal bid, decimal ask)
        => FixFrame("W", sequenceNumber,
        [
            ("262", "REQ1"),
            ("55", "EUR/USD"),
            ("48", "4001"),
            ("268", "2"),
            ("269", "0"),
            ("270", bid.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)),
            ("271", "1000000"),
            ("269", "1"),
            ("270", ask.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)),
            ("271", "1000000")
        ]);

    private static string PipeToSoh(string pipeFix)
        => pipeFix.Replace('|', LmaxFixMarketDataCodec.Soh) + LmaxFixMarketDataCodec.Soh;

    private static (LmaxMarketDataOnlyPreflightConfig Config, JsonDocument Document) ConfigWithExtraTopLevelProperty(string propertyJson)
    {
        var config = ValidConfig();
        var json = AppendTopLevelProperty(SerializeConfig(config), propertyJson);
        return (config, JsonDocument.Parse(json));
    }

    private static string AppendTopLevelProperty(string json, string propertyJson)
        => json.TrimEnd('}', '\r', '\n', ' ') + "," + propertyJson + "}";

    private static string SerializeConfig(LmaxMarketDataOnlyPreflightConfig config)
        => JsonSerializer.Serialize(config, CanonicalRecorderV2Constants.JsonOptions);

    private static async Task WritePackagedCatalogAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, LmaxMarketDataOnlyApprovedInstrumentCatalog.PackagedCatalogFileName), """
        {
          "schema_version": "lmax-market-data-only-approved-instrument-catalog.v1",
          "source": "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/appsettings.json",
          "permission_scope": "M2C1B_EXPLICIT_DEMO_MARKET_DATA_ONLY_SCOPE",
          "instruments": [
            {
              "symbol": "EURUSD",
              "security_id": "4001",
              "security_id_source": "8",
              "lmax_slash_symbol": "EUR/USD",
              "evidence_source": "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/appsettings.json:LmaxConnectivityLab",
              "permission_scope": "M2C1B_EXPLICIT_DEMO_MARKET_DATA_ONLY_SCOPE"
            }
          ]
        }
        """);
    }
    private static LmaxMarketDataOnlyPreflightConfig ValidConfig()
        => new(
            "CAPTURE_ONLY",
            "DEMO",
            "LMAX_DEMO_READ_ONLY",
            "LMAX_DEMO_MARKET_DATA_ONLY",
            "LMAX_DEMO_MD_READ_ONLY",
            "local-secure-store:lmax-demo-md-only",
            "MARKET_DATA_ONLY",
            ["EURUSD"],
            Path.Combine(Path.GetTempPath(), "m2c1b-capture", Guid.NewGuid().ToString("N")),
            300,
            100_000,
            1L * 1024 * 1024 * 1024,
            1,
            1000,
            16 * 1024 * 1024,
            1000,
            ["A", "0", "1", "2", "4", "5", "V"],
            "test-commit",
            "cfg");

    private static LmaxMarketDataOnlyPreflightConfig WithValidHash(LmaxMarketDataOnlyPreflightConfig config)
        => config with { ConfigHash = LmaxMarketDataOnlyConfigHash.Compute(config) };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QQ.Production.Intraday.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo_root_not_found");
    }
}
