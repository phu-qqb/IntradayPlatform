using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

/// <summary>One explicit account owner, one FIX reader, and durable facts across
/// natural cycles. No automatic restart/reconnect or synthetic broker snapshot.</summary>
public sealed class LmaxDemoContinuingSession(
    LmaxConnectivityLabOptions options,
    LmaxDemoSessionStart start,
    ILmaxDemoFixTransport transport,
    ILmaxDemoStrategySession marketData,
    IClock clock,
    string? simulatedJournalRoot = null) : ILmaxDemoStrategySession, IAsyncDisposable
{
    private readonly object stateLock = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly SemaphoreSlim orderWriter = new(1, 1);
    private readonly SemaphoreSlim marketReader = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource<bool> logon = Signal();
    private TaskCompletionSource<bool> changed = Signal();
    private readonly Dictionary<string, Channel<LmaxFixExecutionReport>> routes = new(StringComparer.Ordinal);
    private readonly HashSet<string> parentIds = new(StringComparer.Ordinal);
    private LmaxDemoSessionOwnership? ownership;
    private Task? receiver;
    private Task? heartbeats;
    private int sequence = 1;
    private string? cycleId;
    private bool disconnected;
    private DateTimeOffset lastInbound;
    private LmaxDemoControlledSession State => ownership?.Session ?? throw new InvalidOperationException("DEMO_SESSION_NOT_STARTED");
    private string Sender => options.FixUsername!;
    private string Target => (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (ownership is not null) throw new InvalidOperationException("DEMO_SESSION_ALREADY_STARTED");
        if (start.Simulated != transport.IsSimulated || start.AccountId != LmaxDemoControlledSession.DemoAccountId
            || options.AccountCode != start.AccountId || options.EnvironmentName != "Demo"
            || options.AllowLiveTrading || !options.AllowExternalConnections || !options.AllowOrderSubmission || options.DryRun
            || string.IsNullOrWhiteSpace(Sender) || options.FixSenderCompId != Sender
            || string.IsNullOrWhiteSpace(options.FixPassword) || string.IsNullOrWhiteSpace(Target))
            throw new InvalidOperationException("DEMO_CONTINUING_ACCOUNT_OR_OPTIONS_INVALID");
        if (!start.Simulated && (string.IsNullOrWhiteSpace(start.InternalBrokerAccountCode)
            || start.DeadlineUtc != LmaxDemoDaySchedule.FinalClose(clock.UtcNow)
            || start.Instruments.Any(x => x.Symbol.Length != 6 || !x.Symbol.All(char.IsAsciiLetterUpper))
            || start.Instruments.Select(x => x.SecurityId).Distinct(StringComparer.Ordinal).Count() != start.Instruments.Count))
            throw new InvalidOperationException("DEMO_CONTINUING_DAY_OR_FX_SCOPE_INVALID");
        ownership = LmaxDemoSessionOwnership.Begin(start, clock.UtcNow, simulatedJournalRoot);
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connect.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ConnectTimeoutSeconds + options.LogonTimeoutSeconds)));
            await transport.ConnectAsync(connect.Token);
            await writer.WaitAsync(connect.Token);
            try { await transport.WriteAsync(RawLmaxFixSessionClient.BuildLogonMessage(options, sequence++, Target), connect.Token); }
            finally { writer.Release(); }
            receiver = ReceiveAsync();
            await logon.Task.WaitAsync(connect.Token);
            heartbeats = HeartbeatAsync();
        }
        catch { Block("START_INTERRUPTED_RECONCILIATION_REQUIRED"); throw; }
    }

    public void ValidateContinuity(string? symbol = null)
    {
        lock (stateLock)
        {
            State.RequireContinuity(clock.UtcNow);
            if (symbol is not null && !start.Instruments.Any(x => x.Symbol == symbol))
                throw new InvalidOperationException("DEMO_CONTINUING_SYMBOL_OUTSIDE_OBSERVED_SCOPE");
        }
    }
    public void BeginCycle(string id, IReadOnlyDictionary<string, decimal> targets)
    {
        lock (stateLock)
        {
            if (State.KnownOrders.Any(x => !x.Terminal)) throw new InvalidOperationException("DEMO_PRIOR_WORKING_ORDERS_REQUIRE_RECONCILIATION");
            State.BeginCycle(id, targets, clock.UtcNow);
            cycleId = id;
        }
    }
    public IReadOnlyDictionary<string, decimal> Positions()
    {
        lock (stateLock) { State.RequireContinuity(clock.UtcNow); return State.FillDerivedPositions; }
    }
    public IReadOnlyList<LmaxDemoSessionOrder> Orders()
    {
        lock (stateLock) { State.RequireContinuity(clock.UtcNow); return State.KnownOrders; }
    }
    public IReadOnlyList<LmaxDemoSessionOrder> OrdersForCompletedParent(string parentId)
    {
        lock (stateLock)
        {
            var orders = State.KnownOrders.Where(x => x.Intent.ParentId == parentId).ToArray();
            if (orders.Length == 0 || orders.Any(x => !x.Terminal))
                throw new InvalidOperationException("DEMO_PARENT_FACTS_NOT_TERMINAL");
            return orders;
        }
    }
    public string? BlockingReason { get { lock (stateLock) return State.BlockingReason; } }
    public bool IsClosed { get { lock (stateLock) return State.IsClosed; } }
    public LmaxDemoSessionStart StartingObservation => start;
    public void FinalObservation(DateTimeOffset observedAtUtc, string approvalId, bool flat, bool noOrders)
    {
        lock (stateLock) State.CloseAfterFinalObservation(observedAtUtc, approvalId, flat, noOrders, clock.UtcNow);
    }
    public void Block(string reason)
    {
        lock (stateLock)
        {
            if (ownership is not null && State.BlockingReason is null) State.RecordRuntimeFault(reason, clock.UtcNow);
            Pulse();
        }
    }
    private void Pulse() { var prior = changed; changed = Signal(); prior.TrySetResult(true); }

    private async Task ReceiveAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var frame = await transport.ReadAsync(lifetime.Token);
                try
                {
                    var type = LmaxFixMarketDataCodec.GetMsgType(frame);
                    if (LmaxFixMarketDataCodec.GetTag(frame, "49") != Target || LmaxFixMarketDataCodec.GetTag(frame, "56") != Sender
                        || !long.TryParse(LmaxFixMarketDataCodec.GetTag(frame, "34"), out var inboundSequence) || inboundSequence < 1)
                        throw new InvalidDataException("DEMO_FIX_INBOUND_SESSION_SCOPE_INVALID");
                    if (type is "A" or "0" or "1")
                    {
                        lock (stateLock)
                        {
                            State.RecordInboundControl(inboundSequence, type, clock.UtcNow);
                            lastInbound = clock.UtcNow;
                            Pulse();
                        }
                        if (type == "A") logon.TrySetResult(true);
                        if (type == "1")
                        {
                            var id = LmaxFixMarketDataCodec.GetTag(frame, "112");
                            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("DEMO_FIX_TEST_REQUEST_ID_MISSING");
                            await SendControlAsync("0", [("112", id)], lifetime.Token);
                        }
                    }
                    else if (type == "8")
                    {
                        var securityId = LmaxFixMarketDataCodec.GetTag(frame, "48");
                        var binding = start.Instruments.SingleOrDefault(x => x.SecurityId == securityId)
                            ?? throw new InvalidDataException("DEMO_FIX_UNKNOWN_SECURITY");
                        var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(frame, ForInstrument(binding)).Report;
                        if (normalized.Account != start.AccountId || normalized.FixSequenceNumber is null || normalized.TransactTimeUtc is null
                            || normalized.OrderQty is null || normalized.CumQty is null || normalized.LeavesQty is null
                            || string.IsNullOrWhiteSpace(normalized.ExecId) || string.IsNullOrWhiteSpace(normalized.ClOrdId)
                            || string.IsNullOrWhiteSpace(normalized.OrderId)
                            || NormalizeSymbol(normalized.Symbol) != binding.Symbol)
                            throw new InvalidDataException("DEMO_FIX_REPORT_SCOPE_OR_FIELDS_INVALID");
                        var fact = new Arch7bExecutionReportEvent(start.SessionId, inboundSequence, normalized.Account,
                            normalized.OrderId, normalized.ClOrdId, normalized.OrigClOrdId, normalized.ExecId,
                            normalized.ExecTypeRaw ?? "", normalized.OrdStatusRaw ?? "", binding.Symbol, binding.SecurityId,
                            normalized.SideRaw == "1" ? "BUY" : normalized.SideRaw == "2" ? "SELL" : "INVALID",
                            normalized.OrderQty.Value, normalized.CumQty.Value, normalized.LeavesQty.Value,
                            normalized.LastQty ?? 0m, normalized.LastPx ?? 0m, normalized.AvgPx ?? 0m, normalized.Price,
                            normalized.TransactTimeUtc.Value, normalized.PossDup, normalized.RawMessageSha256);
                        lock (stateLock)
                        {
                            // Durable append and complete account projection precede delivery to a parent.
                            State.RecordExecutionReport(fact, clock.UtcNow);
                            lastInbound = clock.UtcNow;
                            if (!routes.TryGetValue(normalized.ClOrdId, out var mailbox))
                                throw new InvalidDataException("DEMO_FIX_REPORT_ROUTE_MISSING");
                            if (!mailbox.Writer.TryWrite(normalized)) throw new InvalidDataException("DEMO_FIX_REPORT_ROUTE_CLOSED");
                            Pulse();
                        }
                    }
                    else throw new InvalidDataException("DEMO_FIX_UNSUPPORTED_SESSION_TRANSITION");
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // Preserve reception/evidence after a semantic fault; never resume submissions.
                    Block("FIX_PROTOCOL_RECONCILIATION_REQUIRED");
                    logon.TrySetException(new InvalidOperationException("DEMO_FIX_LOGON_OR_PROTOCOL_FAILED"));
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            disconnected = true;
            Block("TRANSPORT_LOST_RECONCILIATION_REQUIRED");
            logon.TrySetException(new IOException("DEMO_FIX_TRANSPORT_LOST"));
        }
    }

    private async Task HeartbeatAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(lifetime.Token))
            {
                if (disconnected) return;
                lock (stateLock)
                    if (clock.UtcNow - lastInbound > LmaxDemoControlledSession.MaximumInboundSilence)
                        Block("INBOUND_CONTINUITY_LOST");
                await SendControlAsync("0", [], lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { Block("HEARTBEAT_WRITE_AMBIGUOUS"); }
    }
    private async Task SendControlAsync(string type, IReadOnlyList<(string Tag, string Value)> fields, CancellationToken token)
    {
        await writer.WaitAsync(token);
        try { await transport.WriteAsync(LmaxFixMarketDataCodec.BuildMessage(type, sequence++, Sender, Target, fields), token); }
        finally { writer.Release(); }
    }
    private async Task WaitForReconciledAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds)));
        while (true)
        {
            Task wait;
            lock (stateLock)
            {
                try { State.RequireContinuity(clock.UtcNow); return; }
                catch (InvalidOperationException error) when (error.Message == "DEMO_SESSION_SEND_OR_CANCEL_UNRESOLVED" && State.BlockingReason is null)
                { wait = changed.Task; }
            }
            try { await wait.WaitAsync(timeout.Token); }
            catch (OperationCanceledException) { Block("ACKNOWLEDGEMENT_UNRESOLVED"); throw; }
        }
    }

    private async Task SendIntentAsync(LmaxDemoStrategyExecutionRequest request, string id, string? original,
        decimal quantity, decimal? price, bool market, Channel<LmaxFixExecutionReport> mailbox,
        LmaxDemoParentLifecycle parent, CancellationToken token)
    {
        await orderWriter.WaitAsync(token);
        try
        {
            await WaitForReconciledAsync(token);
            if (original is null && clock.UtcNow >= request.TargetCloseUtc)
                throw new InvalidOperationException("DEMO_STRATEGY_TARGET_DEADLINE_EXPIRED");
            await writer.WaitAsync(token);
            try
            {
                if (original is null && clock.UtcNow >= request.TargetCloseUtc)
                    throw new InvalidOperationException("DEMO_STRATEGY_TARGET_DEADLINE_EXPIRED");
                string frame;
                lock (stateLock)
                {
                    if (original is null)
                    {
                        var order = new LmaxFixDemoOrderRequest(request.InstrumentSymbol, request.SecurityId, request.Side,
                            market ? LmaxFixDemoOrderType.Market : LmaxFixDemoOrderType.Limit,
                            market ? LmaxFixDemoOrderTimeInForce.IOC : LmaxFixDemoOrderTimeInForce.Day,
                            quantity, price, options.MaxDemoOrderNotionalUsd, id, request.Account, true, false, request.MaxWaitSeconds, false);
                        frame = LmaxFixRecoveryCodec.BuildNewOrderSingle(Sender, Target, sequence++, order, id, options.FixSecurityIdSource);
                        parent.RegisterChild(id, quantity);
                    }
                    else
                    {
                        var known = State.KnownOrders.Single(x => x.Intent.ClientOrderId == original);
                        // A late fill may already be durable but still queued for this parent.
                        // Do not cancel a terminal order or bind the intent to stale leaves.
                        if (known.Terminal) return;
                        quantity = known.LeavesQuantity;
                        frame = LmaxFixRecoveryCodec.BuildOrderCancelRequest(Sender, Target, sequence++, id, original,
                            request.InstrumentSymbol, request.Side == LmaxFixDemoOrderSide.Buy ? "1" : "2",
                            parent.WorkingOrderQuantity, request.SecurityId, options.FixSecurityIdSource);
                        parent.RegisterCancel(id, original);
                    }
                    var intent = new LmaxDemoSessionSendIntent(cycleId!, request.PersistedChildOrderId!, id,
                        original is null ? "D" : "F", original, request.InstrumentSymbol, request.SecurityId,
                        request.Side == LmaxFixDemoOrderSide.Buy ? "BUY" : "SELL", quantity,
                        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(frame))),
                        original is null ? (market ? "1" : "2") : null,
                        original is null ? (market ? "3" : "0") : null, price);
                    State.RecordSendIntent(intent, clock.UtcNow);
                    routes.Add(id, mailbox);
                }
                try
                {
                    if (original is null && clock.UtcNow >= request.TargetCloseUtc)
                        throw new InvalidOperationException("DEMO_STRATEGY_TARGET_DEADLINE_EXPIRED");
                    await transport.WriteAsync(frame, token);
                    lock (stateLock) { State.RecordSendCompleted(id, clock.UtcNow); Pulse(); }
                }
                catch { Block("SEND_AMBIGUOUS_RECONCILIATION_REQUIRED"); throw; }
            }
            finally { writer.Release(); }
        }
        finally { orderWriter.Release(); }
    }

    public async Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(LmaxConnectivityLabOptions requestOptions, TimeSpan maxAge, CancellationToken token)
    {
        await marketReader.WaitAsync(token);
        try { return await marketData.GetTopOfBookAsync(requestOptions, maxAge, token); }
        finally { marketReader.Release(); }
    }
    public async Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(LmaxConnectivityLabOptions requestOptions,
        LmaxDemoStrategyExecutionRequest request, CancellationToken token)
    {
        await WaitForReconciledAsync(token);
        if (!start.Instruments.Any(x => x.Symbol == request.InstrumentSymbol))
            throw new InvalidOperationException("DEMO_CONTINUING_SYMBOL_OUTSIDE_OBSERVED_SCOPE");
        if (request.Account != start.AccountId || request.VenueQuantity <= 0m || (options.DemoOrderCapsEnabled && request.VenueQuantity > options.MaxDemoOrderQuantity)
            || request.TargetKnownAtUtc.Offset != TimeSpan.Zero || request.TargetCloseUtc.Offset != TimeSpan.Zero
            || request.TargetKnownAtUtc > clock.UtcNow || request.TargetCloseUtc <= clock.UtcNow
            || request.TargetCloseUtc > start.DeadlineUtc || request.TargetCloseUtc - request.TargetKnownAtUtc > TimeSpan.FromMinutes(15)
            || request.PriceTickSize <= 0m || !Guid.TryParseExact(request.PersistedChildOrderId, "N", out _)
            || string.IsNullOrWhiteSpace(request.RootClientOrderId) || request.RootClientOrderId.Length > 18
            || request.RootClientOrderId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new InvalidOperationException("DEMO_CONTINUING_PARENT_BINDING_INVALID");
        lock (stateLock)
            if (cycleId is null || !parentIds.Add(request.PersistedChildOrderId!))
                throw new InvalidOperationException("DEMO_CONTINUING_PARENT_REPLAY_BLOCKED");
        var mailbox = Channel.CreateUnbounded<LmaxFixExecutionReport>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var parent = new LmaxDemoParentLifecycle(request.VenueQuantity);
        var reports = new List<LmaxFixExecutionReport>();
        var phases = new List<LmaxDemoStrategyPhase>();
        var quotes = new List<LmaxDemoStrategyQuote>();
        var began = clock.UtcNow;
        var childNumber = 0;
        string Next(string suffix) => request.RootClientOrderId[..Math.Min(14, request.RootClientOrderId.Length)] + suffix + (++childNumber).ToString("00", CultureInfo.InvariantCulture);
        async Task ReadUntil(DateTimeOffset until)
        {
            while (parent.WorkingClientOrderId is not null)
            {
                if (BlockingReason is not null) throw new InvalidOperationException("DEMO_CONTINUING_RECONCILIATION_REQUIRED");
                if (mailbox.Reader.TryRead(out var ready)) { if (parent.Observe(ready)) reports.Add(ready); continue; }
                var remaining = until - clock.UtcNow;
                if (remaining <= TimeSpan.Zero) return;
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
                wait.CancelAfter(remaining);
                try
                {
                    var report = await mailbox.Reader.ReadAsync(wait.Token);
                    if (parent.Observe(report)) reports.Add(report);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { return; }
            }
        }
        async Task Cancel()
        {
            if (parent.WorkingClientOrderId is not { } original) return;
            await SendIntentAsync(request, Next("X"), original, parent.WorkingLeavesQuantity, null, false, mailbox, parent, token);
            await ReadUntil(clock.UtcNow.AddSeconds(Math.Max(1, options.RequestTimeoutSeconds)));
            if (parent.WorkingClientOrderId is not null) throw new InvalidOperationException("DEMO_CONTINUING_CANCEL_UNCONFIRMED");
        }
        async Task Limit(LmaxDemoStrategyPhase phase, DateTimeOffset until)
        {
            var quote = await GetTopOfBookAsync(requestOptions, request.MaxMarketDataAge, token);
            quote.Validate(clock.UtcNow, request.MaxMarketDataAge);
            quotes.Add(quote);
            var price = phase == LmaxDemoStrategyPhase.PassivePosted
                ? LmaxDemoStrategyPolicy.PassivePrice(request.Side, quote, request.PriceTickSize)
                : LmaxDemoStrategyPolicy.Reprice(request.Side, quote, request.PriceTickSize);
            phases.Add(phase);
            await SendIntentAsync(request, Next(phase == LmaxDemoStrategyPhase.PassivePosted ? "P" : "R"), null, parent.RemainingQuantity, price, false, mailbox, parent, token);
            await ReadUntil(until);
        }
        try
        {
            if (clock.UtcNow < request.TargetCloseUtc.AddMinutes(-5))
                await Limit(LmaxDemoStrategyPhase.PassivePosted, request.TargetCloseUtc.AddMinutes(-5));
            if (parent.RemainingQuantity > 0m && clock.UtcNow < request.TargetCloseUtc.AddMinutes(-1))
            {
                await Cancel();
                if (parent.RemainingQuantity > 0m) await Limit(LmaxDemoStrategyPhase.PassiveReprice, request.TargetCloseUtc.AddMinutes(-1));
            }
            if (parent.RemainingQuantity > 0m)
            {
                await Cancel();
                if (parent.RemainingQuantity > 0m)
                {
                    phases.Add(LmaxDemoStrategyPhase.AggressiveResidual);
                    await SendIntentAsync(request, Next("A"), null, parent.RemainingQuantity, null, true, mailbox, parent, token);
                    await ReadUntil(clock.UtcNow.AddSeconds(Math.Max(1, request.MaxWaitSeconds)));
                }
            }
            if (!parent.AllChildrenTerminal) throw new InvalidOperationException("DEMO_CONTINUING_PARENT_NOT_TERMINAL");
            phases.Add(LmaxDemoStrategyPhase.Complete);
            return new(reports, phases, quotes, request.VenueQuantity, parent.CumulativeQuantity, parent.RemainingQuantity,
                true, reports.FirstOrDefault()?.OrderId, began, clock.UtcNow, ["Persistent account session retained"]);
        }
        catch { Block("PARENT_RECONCILIATION_REQUIRED"); throw; }
    }

    private LmaxConnectivityLabOptions ForInstrument(LmaxDemoSessionInstrument instrument)
    {
        var copy = JsonSerializer.Deserialize<LmaxConnectivityLabOptions>(JsonSerializer.Serialize(options))!;
        copy.InstrumentSymbol = instrument.Symbol;
        copy.LmaxInstrumentId = instrument.SecurityId;
        copy.LmaxSlashSymbol = instrument.Symbol[..3] + "/" + instrument.Symbol[3..];
        return copy;
    }
    private static string NormalizeSymbol(string? symbol) => (symbol ?? "").Replace("/", "", StringComparison.Ordinal).ToUpperInvariant();
    public async ValueTask DisposeAsync()
    {
        if (ownership is not null && !IsClosed) Block("OWNER_STOPPED_WITHOUT_FINAL_RECONCILIATION");
        lifetime.Cancel();
        if (receiver is not null) await receiver;
        if (heartbeats is not null) await heartbeats;
        await transport.DisposeAsync();
        ownership?.Dispose();
        lifetime.Dispose();
    }
}

