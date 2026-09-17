using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoContinuingSessionTests
{
    [Fact]
    public async Task InvalidReport_RetainsDiagnosticWithoutCredentialsOrSyntheticExecution()
    {
        await using var f = new Fixture();
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Transport.InvalidReport();
        await Until(() => f.Session.BlockingReason is not null);
        using var journal = LmaxDemoSessionJournal.OpenForInspection(Path.Combine(f.Root, f.Start.SessionId + ".journal.jsonl"));
        var failure = Assert.Single(journal.Entries.Where(x => x.Kind == "ProtocolFailure"));
        Assert.Contains("DEMO_FIX_UNKNOWN_SECURITY", failure.Data);
        Assert.Contains("unmapped-security", failure.Data);
        Assert.DoesNotContain("SIMULATED-SECRET-MARKER", failure.Data);
        Assert.DoesNotContain("SIMULATED-FREE-TEXT", failure.Data);
        Assert.DoesNotContain(journal.Entries, x => x.Kind is "Report" or "SendIntent" or "SendCompleted");
        var inspected = LmaxDemoControlledSession.Inspect(journal);
        Assert.Equal("FIX_PROTOCOL_RECONCILIATION_REQUIRED", inspected.BlockingReason);
        Assert.Empty(inspected.KnownOrders);
        Assert.Throws<InvalidOperationException>(() => f.Session.BeginCycle("unsafe", new Dictionary<string, decimal> { ["EURUSD"] = 1000m }));
    }

    [Fact]
    public void Framing_RetainsEveryFragmentAndCoalescedMessage()
    {
        var one = Frame("0", 1, []);
        var two = Frame("1", 2, [("112", "probe")]);
        var frames = new LmaxDemoFixFrames();
        foreach (var b in Encoding.ASCII.GetBytes(one[..^1]))
        {
            frames.Append([b]);
            Assert.False(frames.TryRead(out _));
        }
        frames.Append(Encoding.ASCII.GetBytes(one[^1..] + two + one[..12]));
        Assert.True(frames.TryRead(out var first));
        Assert.Equal(one, first);
        Assert.True(frames.TryRead(out var second));
        Assert.Equal(two, second);
        Assert.False(frames.TryRead(out _));
        frames.Append(Encoding.ASCII.GetBytes(one[12..]));
        Assert.True(frames.TryRead(out var third));
        Assert.Equal(one, third);
    }

    [Fact]
    public void Framing_RejectsCorruptChecksumWithoutResynchronizingPastIt()
    {
        var frame = Frame("0", 1, []);
        var frames = new LmaxDemoFixFrames();
        frames.Append(Encoding.ASCII.GetBytes(frame.Replace("35=0", "35=1", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => frames.TryRead(out _));
    }

    [Fact]
    public async Task TwoCyclesBeyond900Seconds_ShareSocketAndRetainNonzeroPositions()
    {
        await using var f = new Fixture();
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("first", new Dictionary<string, decimal> { ["EURUSD"] = 1000m });
        await f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("EURUSD", LmaxFixDemoOrderSide.Buy, .1m), CancellationToken.None);
        Assert.Equal(1000m, f.Session.Positions()["EURUSD"]);
        for (var i = 0; i < 16; i++) { f.Clock.Advance(TimeSpan.FromMinutes(1)); await f.Transport.PulseAsync(); }
        Assert.True(f.Clock.UtcNow - f.Start.ObservedAtUtc > TimeSpan.FromSeconds(900));
        f.Session.BeginCycle("second", new Dictionary<string, decimal> { ["EURUSD"] = 0m });
        await f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("EURUSD", LmaxFixDemoOrderSide.Sell, .1m), CancellationToken.None);
        Assert.Equal(0m, f.Session.Positions()["EURUSD"]);
        Assert.Equal(1, f.Transport.ConnectionCount);
        Assert.Equal(1, f.Transport.LogonCount);
        Assert.Equal(2, f.Transport.OrderFrames.Count);
        Assert.Null(f.Session.BlockingReason);
        Assert.Equal(f.Start.ObservedAtUtc, f.Session.StartingObservation.ObservedAtUtc);
        f.Session.FinalObservation(f.Clock.UtcNow, "simulated-final-check", true, true);
        Assert.True(f.Session.IsClosed);
    }

    [Fact]
    public async Task DisabledDemoCaps_SendNaturalTwoContractTargetOnSimulatedTransport()
    {
        await using var f = new Fixture();
        f.Options.DemoOrderCapsEnabled = false;
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("natural", new Dictionary<string, decimal> { ["EURUSD"] = -20000m });
        await f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("EURUSD", LmaxFixDemoOrderSide.Sell, 2m), CancellationToken.None);
        Assert.Equal(-20000m, f.Session.Positions()["EURUSD"]);
        Assert.Single(f.Transport.OrderFrames);
        Assert.Null(f.Session.BlockingReason);
    }

    [Fact]
    public async Task DefaultDemoCap_RejectsTwoContractsBeforeSimulatedSend()
    {
        await using var f = new Fixture();
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("natural", new Dictionary<string, decimal> { ["EURUSD"] = -20000m });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Session.ExecuteStrategyParentAsync(f.Options,
            f.Request("EURUSD", LmaxFixDemoOrderSide.Sell, 2m), CancellationToken.None));
        Assert.Empty(f.Transport.OrderFrames);
    }

    [Fact]
    public async Task WorkingPartialAndDuplicate_RemainVisibleAndBlockAnotherCycle()
    {
        await using var f = new Fixture();
        f.Transport.AutoFill = false;
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("first", new Dictionary<string, decimal> { ["EURUSD"] = 1000m });
        var running = f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("EURUSD", LmaxFixDemoOrderSide.Buy, .1m), CancellationToken.None);
        await Until(() => f.Transport.OrderFrames.Count == 1);
        var order = f.Transport.OrderFrames.Single();
        var partial = f.Transport.Report(order, "partial", "F", "1", .04m, .06m, .04m);
        await f.Transport.PulseAsync();
        Assert.Equal(400m, f.Session.Positions()["EURUSD"]);
        Assert.Equal(.06m, Assert.Single(f.Session.Orders()).LeavesQuantity);
        Assert.Throws<InvalidOperationException>(() => f.Session.BeginCycle("unsafe", new Dictionary<string, decimal> { ["EURUSD"] = 2000m }));
        f.Transport.Duplicate(partial);
        await f.Transport.PulseAsync();
        Assert.Equal(400m, f.Session.Positions()["EURUSD"]);
        f.Transport.Report(order, "remaining", "F", "2", .1m, 0m, .06m);
        var result = await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(.1m, result.CumulativeQuantity);
        Assert.Equal(2, result.ExecutionReports.Count(x => x.ExecType == LmaxFixExecutionReportType.Trade));
        Assert.All(f.Session.Orders(), x => Assert.True(x.Terminal));
    }

    [Fact]
    public async Task ParallelParents_WaitForAcknowledgementsAndUseUniqueIdentities()
    {
        await using var f = new Fixture();
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("basket", new Dictionary<string, decimal> { ["EURUSD"] = 1000m, ["GBPUSD"] = -1000m });
        await Task.WhenAll(
            f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("EURUSD", LmaxFixDemoOrderSide.Buy, .1m), CancellationToken.None),
            f.Session.ExecuteStrategyParentAsync(f.Options, f.Request("GBPUSD", LmaxFixDemoOrderSide.Sell, .1m), CancellationToken.None));
        Assert.Equal(2, f.Transport.OrderFrames.Select(x => Tag(x, "11")).Distinct().Count());
        Assert.Equal(1000m, f.Session.Positions()["EURUSD"]);
        Assert.Equal(-1000m, f.Session.Positions()["GBPUSD"]);
        Assert.Null(f.Session.BlockingReason);
    }

    [Fact]
    public async Task AmbiguousWrite_IsDurableAndNeverRetriedInAnotherSession()
    {
        await using var f = new Fixture();
        f.Transport.FailOrderWrite = true;
        f.Transport.BeforeOrderWrite = () =>
        {
            using var journal = LmaxDemoSessionJournal.OpenForInspection(Path.Combine(f.Root, f.Start.SessionId + ".journal.jsonl"));
            Assert.Single(journal.Entries.Where(x => x.Kind == "SendIntent"));
        };
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("ambiguous", new Dictionary<string, decimal> { ["EURUSD"] = 1000m });
        await Assert.ThrowsAsync<IOException>(() => f.Session.ExecuteStrategyParentAsync(f.Options,
            f.Request("EURUSD", LmaxFixDemoOrderSide.Buy, .1m), CancellationToken.None));
        Assert.NotNull(f.Session.BlockingReason);
        Assert.Throws<InvalidOperationException>(() => f.Session.BeginCycle("retry", new Dictionary<string, decimal> { ["EURUSD"] = 1000m }));
        Assert.Single(f.Transport.OrderFrames);
        await f.Session.DisposeAsync();
        f.Disposed = true;
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(f.Start with { SessionId = "renamed" }, f.Clock.UtcNow, f.Root));
    }

    [Fact]
    public void AccountOwnership_PreventsParallelOwnerAndUnclosedNewFilename()
    {
        var root = Path.Combine(Path.GetTempPath(), "simulated-demo-owner-" + Guid.NewGuid().ToString("N"));
        var now = new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);
        var start = Start(now);
        using (var owner = LmaxDemoSessionOwnership.Begin(start, now, root))
            Assert.Throws<IOException>(() => LmaxDemoSessionOwnership.Begin(start with { SessionId = "parallel" }, now, root));
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(start with { SessionId = "renamed" }, now, root));
        Directory.Delete(root, true);
    }

    [Fact]
    public void CancelAcknowledgementMayArriveBeforeSocketCompletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "simulated-demo-ack-" + Guid.NewGuid().ToString("N"));
        var now = new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);
        var start = Start(now);
        using (var owner = LmaxDemoSessionOwnership.Begin(start, now, root))
        {
            var s = owner.Session;
            s.RecordInboundControl(1, "A", now);
            s.BeginCycle("c", new Dictionary<string, decimal> { ["EURUSD"] = 1000m }, now);
            s.RecordSendIntent(new("c", "parent", "order", "D", null, "EURUSD", "4001", "BUY", .1m, new string('A', 64)), now);
            var ack = new Arch7bExecutionReportEvent(start.SessionId, 2, start.AccountId, "broker", "order", null, "ack", "0", "0",
                "EURUSD", "4001", "BUY", .1m, 0m, .1m, 0m, 0m, 0m, null, now, false, new string('B', 64));
            s.RecordExecutionReport(ack, now);
            s.RecordSendCompleted("order", now);
            s.RecordSendIntent(new("c", "parent", "cancel", "F", "order", "EURUSD", "4001", "BUY", .1m, new string('C', 64)), now);
            s.RecordExecutionReport(ack with { SequenceNumber = 3, ClOrdId = "cancel", OrigClOrdId = "order", ExecId = "canceled", ExecType = "4", OrdStatus = "4", LeavesQty = 0m }, now);
            s.RecordSendCompleted("cancel", now);
            s.RequireContinuity(now);
            Assert.Null(s.BlockingReason);
        }
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task PartialCancelAndResidual_PersistAsSeparatePhysicalChildrenWithoutInventedAcknowledgement()
    {
        await using var f = new Fixture(simulatedQuotes: true);
        f.Transport.AutoFill = false;
        f.Transport.AutoFillMarkets = true;
        await f.Session.InitializeAsync(CancellationToken.None);
        f.Session.BeginCycle("physical", new Dictionary<string, decimal> { ["EURUSD"] = 1000m });
        var request = f.Request("EURUSD", LmaxFixDemoOrderSide.Buy, .1m) with { TargetCloseUtc = f.Clock.UtcNow.AddSeconds(61) };
        var running = f.Session.ExecuteStrategyParentAsync(f.Options, request, CancellationToken.None);
        await Until(() => f.Transport.OrderFrames.Count == 1);
        f.Transport.Report(f.Transport.OrderFrames.Single(), "partial-physical", "F", "1", .04m, .06m, .04m);
        var result = await running.WaitAsync(TimeSpan.FromSeconds(8));
        var state = SeedData.Create(f.Clock.UtcNow);
        var run = state.ModelRuns.Single();
        var instrument = state.Instruments.Single();
        var venue = state.Venues.Single();
        var intent = new TradeIntent(TradeIntentId.New(), run.Id, run.FundId, instrument.Id, TradeSide.Buy,
            1000m, .1m, "simulated natural target", TradeIntentStatus.Created, f.Clock.UtcNow);
        var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new ClientOrderId("simulated-parent"),
            OrderSide.Buy, 1000m, ExecutionAlgo.CloseSeeking15m, OrderStatus.Created, f.Clock.UtcNow);
        var initial = new ChildOrder(new ChildOrderId(Guid.ParseExact(request.PersistedChildOrderId!, "N")), parent.Id,
            venue.Id, new ClientOrderId("persisted-before-send"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC,
            1000m, .1m, OrderStatus.PendingNew, f.Clock.UtcNow);
        state.TradeIntents.Add(intent);
        state.ParentOrders.Add(parent);
        state.ChildOrders.Add(initial);
        var mapped = LmaxDemoPhysicalExecutionMapper.Map(initial, 10000m, state.BrokerAccounts.Single().AccountCode,
            f.Start, f.Session.OrdersForCompletedParent(request.PersistedChildOrderId!), result);
        Assert.Equal(2, mapped.PhysicalChildren.Count);
        Assert.Contains(mapped.PhysicalChildren, x => x.Status == OrderStatus.Cancelled && x.VenueQuantity == .1m);
        Assert.Contains(mapped.PhysicalChildren, x => x.Status == OrderStatus.Filled && x.VenueQuantity == .06m);
        Assert.DoesNotContain(mapped.Reports, x => x.ExecutionReportType == ExecutionReportType.OrderAck);
        var fills = mapped.Reports.Where(x => x.ExecutionReportType is ExecutionReportType.Fill or ExecutionReportType.PartialFill)
            .Select(x => new Fill(FillId.New(), x.BrokerExecutionId!, x.ChildOrderId, instrument.Id, venue.Id, TradeSide.Buy,
                x.LastQuantity * 10000m, x.LastQuantity, x.LastPrice, x.ReceivedAtUtc, x.ReceivedAtUtc)).ToArray();
        mapped = mapped with { Fills = fills, Ledger = fills.Select(x => new PositionLedgerEvent(Guid.NewGuid(), run.FundId,
            instrument.Id, PositionLedgerEventType.Fill, x.BaseQuantity, x.BrokerExecutionId, x.ReceivedAtUtc)).ToArray() };
        var repository = new InMemoryIntradayRepository(state);
        await repository.PersistDemoParentAsync(mapped, CancellationToken.None);
        await repository.PersistDemoParentAsync(mapped, CancellationToken.None);
        Assert.Equal(2, state.ChildOrders.Count);
        Assert.Equal(2, state.Fills.Count);
        Assert.Equal(1000m, state.PositionLedger.Sum(x => x.BaseQuantityDelta));
        Assert.Equal(f.Session.Positions()["EURUSD"], state.PositionLedger.Sum(x => x.BaseQuantityDelta));
        Assert.Equal(OrderStatus.Filled, state.ParentOrders.Single().Status);
    }

    private static async Task Until(Func<bool> predicate)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(5, limit.Token);
    }
    private static string Tag(string frame, string tag) => LmaxFixMarketDataCodec.GetTag(frame, tag)!;
    private static string Frame(string type, int sequence, IReadOnlyList<(string Tag, string Value)> fields)
        => LmaxFixMarketDataCodec.BuildMessage(type, sequence, "LMAX", "SIMULATED", fields);
    private static LmaxDemoSessionStart Start(DateTimeOffset now) => new("simulated-" + Guid.NewGuid().ToString("N"),
        "1754288005", "Demo", "simulated-owner-ui-check", now, now.AddHours(5), true, true, true, true,
        [new("EURUSD", "4001", 10000m), new("GBPUSD", "4002", 10000m)]);

    private sealed class MovingClock(DateTimeOffset now) : IClock
    {
        private long ticks = now.UtcTicks;
        public DateTimeOffset UtcNow => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan amount) => Interlocked.Add(ref ticks, amount.Ticks);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "simulated-demo-runtime-" + Guid.NewGuid().ToString("N"));
        public MovingClock Clock { get; } = new(new DateTimeOffset(2026, 9, 16, 13, 0, 0, TimeSpan.Zero));
        public LmaxConnectivityLabOptions Options { get; } = new() { EnvironmentName = "Demo", AccountCode = "1754288005",
            AllowExternalConnections = true, AllowOrderSubmission = true, AllowLiveTrading = false, DryRun = false,
            FixUsername = "SIMULATED", FixSenderCompId = "SIMULATED", FixPassword = "SIMULATED-NO-SECRET",
            FixTargetCompId = "LMAX", FixOrderTargetCompId = "LMAX", RequestTimeoutSeconds = 5, MaxDemoOrderQuantity = .1m };
        public LmaxDemoSessionStart Start { get; }
        public SimulatedTransport Transport { get; }
        public LmaxDemoContinuingSession Session { get; }
        public bool Disposed { get; set; }
        public Fixture(bool simulatedQuotes = false)
        {
            Start = LmaxDemoContinuingSessionTests.Start(Clock.UtcNow);
            Transport = new SimulatedTransport(Clock);
            Session = new(Options, Start, Transport, simulatedQuotes ? new SimulatedQuotes(Clock) : new NoMarketAccess(), Clock, Root);
        }
        public LmaxDemoStrategyExecutionRequest Request(string symbol, LmaxFixDemoOrderSide side, decimal quantity)
        {
            var child = Guid.NewGuid().ToString("N");
            return new(symbol, symbol == "EURUSD" ? "4001" : "4002", symbol[..3] + "/" + symbol[3..], side,
                quantity, .00001m, "DS" + child[..16], "1754288005", Clock.UtcNow, Clock.UtcNow.AddSeconds(30), TimeSpan.FromMinutes(1), 5, false, child);
        }
        public async ValueTask DisposeAsync()
        {
            if (!Disposed) await Session.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private sealed class SimulatedQuotes(MovingClock clock) : ILmaxDemoStrategySession
    {
        public Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(LmaxConnectivityLabOptions o, TimeSpan age, CancellationToken token)
            => Task.FromResult(new LmaxDemoStrategyQuote(1.0999m, 1.1001m, 1.1m, clock.UtcNow));
        public Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(LmaxConnectivityLabOptions o, LmaxDemoStrategyExecutionRequest r, CancellationToken t)
            => throw new NotSupportedException();
    }
    private sealed class NoMarketAccess : ILmaxDemoStrategySession
    {
        public Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(LmaxConnectivityLabOptions o, TimeSpan age, CancellationToken token) => throw new InvalidOperationException("SIMULATION_MUST_NOT_REQUEST_MARKET_DATA");
        public Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(LmaxConnectivityLabOptions o, LmaxDemoStrategyExecutionRequest r, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class SimulatedTransport(MovingClock clock) : ILmaxDemoFixTransport
    {
        private readonly Channel<string> inbound = Channel.CreateUnbounded<string>();
        public void InvalidReport() => Emit("8", [("48", "unmapped-security"), ("553", "SIMULATED-SECRET-MARKER"),
            ("554", "SIMULATED-SECRET-MARKER"), ("58", "SIMULATED-FREE-TEXT")]);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> probes = new();
        private int sequence;
        public bool IsSimulated => true;
        public int ConnectionCount { get; private set; }
        public int LogonCount { get; private set; }
        public bool AutoFill { get; set; } = true;
        public bool AutoFillMarkets { get; set; }
        private readonly ConcurrentDictionary<string, decimal> cumulative = new();
        public bool FailOrderWrite { get; set; }
        public Action? BeforeOrderWrite { get; set; }
        public ConcurrentQueue<string> OrderFrames { get; } = new();
        public Task ConnectAsync(CancellationToken token) { ConnectionCount++; return Task.CompletedTask; }
        public async Task<string> ReadAsync(CancellationToken token) => await inbound.Reader.ReadAsync(token);
        public Task WriteAsync(string frame, CancellationToken token)
        {
            var type = Tag(frame, "35");
            if (type == "A") { LogonCount++; Emit("A", [("98", "0"), ("108", "30")]); }
            if (type == "0" && LmaxFixMarketDataCodec.GetTag(frame, "112") is { } id && probes.TryRemove(id, out var signal)) signal.TrySetResult(true);
            if (type == "D")
            {
                BeforeOrderWrite?.Invoke();
                OrderFrames.Enqueue(frame);
                if (FailOrderWrite) throw new IOException("SIMULATED_PARTIAL_SOCKET_WRITE");
                var qty = decimal.Parse(Tag(frame, "38"), CultureInfo.InvariantCulture);
                if (AutoFill || AutoFillMarkets && Tag(frame, "40") == "1") Report(frame, "fill-" + Tag(frame, "11"), "F", "2", qty, 0m, qty);
            }
            if (type == "F")
            {
                var original = OrderFrames.Single(x => Tag(x, "11") == Tag(frame, "41"));
                var qty = decimal.Parse(Tag(original, "38"), CultureInfo.InvariantCulture);
                var cum = cumulative.GetValueOrDefault(Tag(original, "11"));
                Emit("8", [("1", "1754288005"), ("37", "broker-" + Tag(original, "11")), ("11", Tag(frame, "11")),
                    ("41", Tag(original, "11")), ("17", "canceled-" + Tag(frame, "11")), ("150", "4"), ("39", "4"),
                    ("48", Tag(original, "48")), ("55", Tag(original, "55")), ("54", Tag(original, "54")),
                    ("38", qty.ToString(CultureInfo.InvariantCulture)), ("14", cum.ToString(CultureInfo.InvariantCulture)),
                    ("151", "0"), ("32", "0"), ("31", "0"), ("6", "1.1"),
                    ("60", clock.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture))]);
            }
            return Task.CompletedTask;
        }
        public async Task PulseAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            probes.AddOrUpdate(id, done, (_, _) => done);
            Emit("1", [("112", id)]);
            await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        private string Emit(string type, IReadOnlyList<(string Tag, string Value)> fields)
        {
            var frame = Frame(type, Interlocked.Increment(ref sequence), fields);
            if (!inbound.Writer.TryWrite(frame)) throw new IOException("SIMULATED_READER_CLOSED");
            return frame;
        }
        public string Report(string order, string execution, string type, string status, decimal cum, decimal leaves, decimal last)
        {
            cumulative[Tag(order, "11")] = cum;
            return Emit("8", [("1", "1754288005"), ("37", "broker-" + Tag(order, "11")), ("11", Tag(order, "11")), ("17", execution),
                ("150", type), ("39", status), ("48", Tag(order, "48")), ("55", Tag(order, "55")), ("54", Tag(order, "54")),
                ("38", Tag(order, "38")), ("14", cum.ToString(CultureInfo.InvariantCulture)), ("151", leaves.ToString(CultureInfo.InvariantCulture)),
                ("32", last.ToString(CultureInfo.InvariantCulture)), ("31", "1.1"), ("6", "1.1"), ("60", clock.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture))]);
        }
        public void Duplicate(string original)
        {
            var excluded = new HashSet<string> { "8", "9", "10", "35", "34", "49", "56", "52", "43" };
            var fields = original.Split('\u0001', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2))
                .Where(x => !excluded.Contains(x[0])).Select(x => (Tag: x[0], Value: x[1])).Append(("43", "Y")).ToArray();
            Emit("8", fields);
        }
        public ValueTask DisposeAsync() { inbound.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
