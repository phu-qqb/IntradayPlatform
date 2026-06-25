extern alias M2C1A;
extern alias M2C1ATool;

using System.Reflection;
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
    public async Task T11_preflight_cli_writes_reports_without_network()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1a-preflight", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "config.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(ValidConfig() with { OutputRoot = Path.Combine(root, "capture") }, CanonicalRecorderV2Constants.JsonOptions));
        var output = Path.Combine(root, "out");
        var exit = await LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(["preflight", "--config", configPath, "--output", output, "--network-disabled", "--no-order-entry", "--no-account-api", "--no-db"]);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(output, "m2c1a_preflight_report.json")));
        Assert.True(File.Exists(Path.Combine(output, "m2c1a_dependency_gate.json")));
        Assert.True(File.Exists(Path.Combine(output, "m2c1b_operator_command.txt")));
        Assert.Contains("DO NOT RUN IN M2C1A", await File.ReadAllTextAsync(Path.Combine(output, "m2c1b_operator_command.txt")));
    }

    [Fact]
    public async Task T12_recorder_accepts_capture_only_events_and_replay_has_exact_counts()
    {
        var root = Path.Combine(Path.GetTempPath(), "m2c1a-recorder", Guid.NewGuid().ToString("N"));
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), 1_000_000);
        await using var recorder = await CanonicalRecorderV2.CreateAsync(new CanonicalRecorderV2Options(root, "m2c1a-run", "DEMO", "test", "git", "c1769aa", "cfg", ["LMAX_MARKET_DATA_CAPTURE_ONLY"], ["EURUSD"], [], [], [], FlushInterval: TimeSpan.FromMilliseconds(1)), clock);
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
        var frame = System.Text.Encoding.ASCII.GetBytes(Fix($"35={msgType}", "34=1"));

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
        var frame = System.Text.Encoding.ASCII.GetBytes(Fix($"35={msgType}", "34=1"));

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

    private static IReadOnlyList<LmaxMarketDataOnlyInstrument> Instruments =>
    [
        new("4001", "EURUSD", "EUR/USD"),
        new("4007", "AUDUSD", "AUD/USD")
    ];

    private static LmaxMarketDataOnlyRawFixFrame Frame(string message)
        => new(message, new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), 1234);

    private static string Fix(params string[] fields)
        => PipeToSoh(string.Join('|', fields));

    private static string PipeToSoh(string pipeFix)
        => pipeFix.Replace('|', LmaxFixMarketDataCodec.Soh) + LmaxFixMarketDataCodec.Soh;

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
            Path.Combine(Path.GetTempPath(), "m2c1a-capture", Guid.NewGuid().ToString("N")),
            300,
            100_000,
            1L * 1024 * 1024 * 1024,
            10L * 1024 * 1024 * 1024,
            1000,
            16 * 1024 * 1024,
            1000,
            ["A", "0", "1", "2", "4", "5", "V"],
            "test-commit",
            "cfg");

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


