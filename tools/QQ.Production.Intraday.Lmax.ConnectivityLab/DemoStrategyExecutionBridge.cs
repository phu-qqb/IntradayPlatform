namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Net.Sockets;
using QQ.Production.Intraday.Application;

public enum LmaxDemoStrategyPhase
{
    PassivePosted,
    PassiveReprice,
    AggressiveResidual,
    Complete
}

public sealed record LmaxDemoStrategyQuote(decimal BestBid, decimal BestAsk, decimal Mid, DateTimeOffset ObservedAtUtc)
{
    public void Validate(DateTimeOffset nowUtc, TimeSpan maxAge)
    {
        if (ObservedAtUtc.Offset != TimeSpan.Zero || ObservedAtUtc > nowUtc || nowUtc - ObservedAtUtc > maxAge)
            throw new InvalidOperationException("DEMO_STRATEGY_MARKET_DATA_NOT_FRESH");
        if (BestBid <= 0m || BestAsk <= BestBid || Mid <= 0m)
            throw new InvalidOperationException("DEMO_STRATEGY_BBO_INVALID");
    }
}

public sealed record LmaxDemoStrategyExecutionRequest(
    string InstrumentSymbol,
    string SecurityId,
    string SlashSymbol,
    LmaxFixDemoOrderSide Side,
    decimal VenueQuantity,
    decimal PriceTickSize,
    string RootClientOrderId,
    string? Account,
    DateTimeOffset TargetKnownAtUtc,
    DateTimeOffset TargetCloseUtc,
    TimeSpan MaxMarketDataAge,
    int MaxWaitSeconds,
    bool ShowFixMessages);

public sealed record LmaxDemoStrategyExecutionResult(
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports,
    IReadOnlyList<LmaxDemoStrategyPhase> Phases,
    IReadOnlyList<LmaxDemoStrategyQuote> Quotes,
    decimal RequestedQuantity,
    decimal CumulativeQuantity,
    decimal LeavesQuantity,
    bool Terminal,
    string? BrokerOrderId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<string> Diagnostics);

public interface ILmaxDemoStrategySession
{
    Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(
        LmaxConnectivityLabOptions options,
        TimeSpan maxAge,
        CancellationToken cancellationToken);

    Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(
        LmaxConnectivityLabOptions options,
        LmaxDemoStrategyExecutionRequest request,
        CancellationToken cancellationToken);
}

public static class LmaxDemoStrategyPolicy
{
    public static LmaxDemoStrategyPhase Phase(DateTimeOffset nowUtc, DateTimeOffset closeUtc)
    {
        if (nowUtc >= closeUtc.AddMinutes(-1)) return LmaxDemoStrategyPhase.AggressiveResidual;
        if (nowUtc >= closeUtc.AddMinutes(-5)) return LmaxDemoStrategyPhase.PassiveReprice;
        return LmaxDemoStrategyPhase.PassivePosted;
    }

    public static decimal PassivePrice(LmaxFixDemoOrderSide side, LmaxDemoStrategyQuote quote, decimal tick)
        => RoundToTick(side == LmaxFixDemoOrderSide.Buy ? quote.BestBid : quote.BestAsk, tick);

    public static decimal Reprice(LmaxFixDemoOrderSide side, LmaxDemoStrategyQuote quote, decimal tick)
    {
        var mid = (quote.BestBid + quote.BestAsk) / 2m;
        var rounded = RoundToTick(mid, tick);
        return side == LmaxFixDemoOrderSide.Buy
            ? Math.Min(rounded, quote.BestAsk)
            : Math.Max(rounded, quote.BestBid);
    }

    private static decimal RoundToTick(decimal price, decimal tick)
    {
        if (tick <= 0m) throw new ArgumentOutOfRangeException(nameof(tick));
        return Math.Round(price / tick, 0, MidpointRounding.AwayFromZero) * tick;
    }
}

public sealed partial class RawLmaxFixSessionClient : ILmaxDemoStrategySession
{
    public async Task<LmaxDemoStrategyQuote> GetTopOfBookAsync(
        LmaxConnectivityLabOptions options,
        TimeSpan maxAge,
        CancellationToken cancellationToken)
    {
        EnsureStrictDemoStrategyOptions(options);
        var marketOptions = CopyForMarketData(options);
        var result = await MarketDataSnapshotSmokeAsync(marketOptions, cancellationToken);
        if (result.Status != "Ok" || !result.CompleteTopOfBook || result.BestBid is null || result.BestAsk is null || result.Mid is null)
            throw new InvalidOperationException("DEMO_STRATEGY_BBO_UNAVAILABLE");
        var observedAt = result.ObservationCompletedAtUtc ?? result.CompletedAtUtc;
        var quote = new LmaxDemoStrategyQuote(result.BestBid.Value, result.BestAsk.Value, result.Mid.Value, observedAt.ToUniversalTime());
        quote.Validate(DateTimeOffset.UtcNow, maxAge);
        return quote;
    }

    public async Task<LmaxDemoStrategyExecutionResult> ExecuteStrategyParentAsync(
        LmaxConnectivityLabOptions options,
        LmaxDemoStrategyExecutionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureStrictDemoStrategyOptions(options);
        ValidateStrategyRequest(options, request);
        var startedAt = DateTimeOffset.UtcNow;
        var phases = new List<LmaxDemoStrategyPhase>();
        var quotes = new List<LmaxDemoStrategyQuote>();
        var reports = new List<LmaxFixExecutionReport>();
        var diagnostics = new List<string>();
        var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var sender = options.FixUsername!;
        var sideFix = request.Side == LmaxFixDemoOrderSide.Buy ? "1" : "2";
        var sequenceNumber = 1;
        decimal cumQty = 0m;
        decimal leavesQty = request.VenueQuantity;
        string? brokerOrderId = null;
        string? workingClOrdId = null;
        var childSequence = 0;

        using var tcp = new TcpClient();
        using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            await tcp.ConnectAsync(options.FixOrderHost!, options.FixOrderPort!.Value, connectTimeout.Token);
        Stream rawStream;
        using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            rawStream = options.UseTls
                ? await CreateTlsStreamAsync(tcp, options.FixOrderHost!, options.CheckCertificateRevocation, connectTimeout.Token)
                : tcp.GetStream();
        await using var stream = rawStream;

        var logon = BuildLogonMessage(options, sequenceNumber++, target);
        using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
        {
            await WriteAsciiAsync(stream, logon, logonTimeout.Token);
            var response = await ReadFixResponseAsync(stream, logonTimeout.Token);
            if (!LmaxFixMarketDataCodec.ContainsTag(response, "35", "A"))
                throw new IOException("DEMO_STRATEGY_FIX_LOGON_NOT_CONFIRMED");
        }

        async Task ReadUntilAsync(DateTimeOffset deadlineUtc, bool stopOnCancel)
        {
            while (DateTimeOffset.UtcNow < deadlineUtc && leavesQty > 0m && !cancellationToken.IsCancellationRequested)
            {
                var remaining = deadlineUtc - DateTimeOffset.UtcNow;
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                wait.CancelAfter(remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining);
                try
                {
                    var (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token, null, sender);
                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) return;
                    var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (msgType == "8")
                    {
                        var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options).Report;
                        reports.Add(normalized);
                        brokerOrderId ??= normalized.OrderId;
                        if (normalized.CumQty.HasValue) cumQty = Math.Max(cumQty, normalized.CumQty.Value);
                        if (normalized.LeavesQty.HasValue) leavesQty = Math.Max(0m, normalized.LeavesQty.Value);
                        if (normalized.OrdStatus is LmaxFixOrderStatus.Filled or LmaxFixOrderStatus.Rejected or LmaxFixOrderStatus.Expired)
                            return;
                        if (stopOnCancel && normalized.OrdStatus == LmaxFixOrderStatus.Canceled)
                            return;
                    }
                    else if (msgType == "3" || msgType == "5")
                    {
                        throw new IOException($"DEMO_STRATEGY_FIX_SESSION_TERMINATED_{msgType}");
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        async Task SubmitLimitAsync(LmaxDemoStrategyPhase phase, decimal price, DateTimeOffset untilUtc)
        {
            phases.Add(phase);
            childSequence++;
            workingClOrdId = ChildClOrdId(request.RootClientOrderId, childSequence, phase == LmaxDemoStrategyPhase.PassivePosted ? "P" : "R");
            var child = new LmaxFixDemoOrderRequest(
                request.InstrumentSymbol, request.SecurityId, request.Side,
                LmaxFixDemoOrderType.Limit, LmaxFixDemoOrderTimeInForce.Day,
                leavesQty, price, options.MaxDemoOrderNotionalUsd, workingClOrdId,
                request.Account, true, false, request.MaxWaitSeconds, request.ShowFixMessages);
            var fix = LmaxFixRecoveryCodec.BuildNewOrderSingle(sender, target, sequenceNumber++, child, workingClOrdId, options.FixSecurityIdSource);
            await WriteAsciiAsync(stream, fix, cancellationToken);
            diagnostics.Add($"{phase}: NewOrderSingle sent; quantity={leavesQty}; price={price}");
            await ReadUntilAsync(untilUtc, stopOnCancel: false);
        }

        async Task CancelWorkingAsync()
        {
            if (string.IsNullOrWhiteSpace(workingClOrdId) || leavesQty <= 0m) return;
            var cancelId = ChildClOrdId(request.RootClientOrderId, ++childSequence, "X");
            var cancel = LmaxFixRecoveryCodec.BuildOrderCancelRequest(
                sender, target, sequenceNumber++, cancelId, workingClOrdId,
                request.InstrumentSymbol, sideFix, leavesQty, request.SecurityId, options.FixSecurityIdSource);
            await WriteAsciiAsync(stream, cancel, cancellationToken);
            diagnostics.Add($"Cancel sent for residual={leavesQty}");
            await ReadUntilAsync(DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, options.RequestTimeoutSeconds)), stopOnCancel: true);
            workingClOrdId = null;
        }

        async Task SubmitResidualAsync()
        {
            if (leavesQty <= 0m) return;
            phases.Add(LmaxDemoStrategyPhase.AggressiveResidual);
            childSequence++;
            var residualId = ChildClOrdId(request.RootClientOrderId, childSequence, "A");
            var residual = new LmaxFixDemoOrderRequest(
                request.InstrumentSymbol, request.SecurityId, request.Side,
                LmaxFixDemoOrderType.Market, LmaxFixDemoOrderTimeInForce.IOC,
                leavesQty, null, options.MaxDemoOrderNotionalUsd, residualId,
                request.Account, true, false, request.MaxWaitSeconds, request.ShowFixMessages);
            var fix = LmaxFixRecoveryCodec.BuildNewOrderSingle(sender, target, sequenceNumber++, residual, residualId, options.FixSecurityIdSource);
            await WriteAsciiAsync(stream, fix, cancellationToken);
            diagnostics.Add($"Aggressive residual sent; quantity={leavesQty}");
            await ReadUntilAsync(DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, request.MaxWaitSeconds)), stopOnCancel: false);
        }

        var now = DateTimeOffset.UtcNow;
        var phase = LmaxDemoStrategyPolicy.Phase(now, request.TargetCloseUtc);
        if (phase == LmaxDemoStrategyPhase.PassivePosted)
        {
            var quote = await GetTopOfBookAsync(options, request.MaxMarketDataAge, cancellationToken);
            quotes.Add(quote);
            await SubmitLimitAsync(phase, LmaxDemoStrategyPolicy.PassivePrice(request.Side, quote, request.PriceTickSize), request.TargetCloseUtc.AddMinutes(-5));
        }

        if (leavesQty > 0m && DateTimeOffset.UtcNow < request.TargetCloseUtc.AddMinutes(-1))
        {
            await CancelWorkingAsync();
            var quote = await GetTopOfBookAsync(options, request.MaxMarketDataAge, cancellationToken);
            quotes.Add(quote);
            await SubmitLimitAsync(LmaxDemoStrategyPhase.PassiveReprice, LmaxDemoStrategyPolicy.Reprice(request.Side, quote, request.PriceTickSize), request.TargetCloseUtc.AddMinutes(-1));
        }

        if (leavesQty > 0m)
        {
            await CancelWorkingAsync();
            await SubmitResidualAsync();
        }

        phases.Add(LmaxDemoStrategyPhase.Complete);
        try
        {
            await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "DemoStrategy", sender, CancellationToken.None);
        }
        catch { }

        return new LmaxDemoStrategyExecutionResult(
            reports, phases, quotes, request.VenueQuantity, cumQty, leavesQty,
            leavesQty == 0m || reports.Any(x => x.OrdStatus is LmaxFixOrderStatus.Filled or LmaxFixOrderStatus.Rejected or LmaxFixOrderStatus.Expired),
            brokerOrderId, startedAt, DateTimeOffset.UtcNow, diagnostics);
    }

    private static void EnsureStrictDemoStrategyOptions(LmaxConnectivityLabOptions options)
    {
        if (!options.EnvironmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEMO_STRATEGY_ENVIRONMENT_MUST_BE_DEMO");
        if (options.AllowLiveTrading)
            throw new InvalidOperationException("DEMO_STRATEGY_LIVE_TRADING_FORBIDDEN");
        if (!options.AllowExternalConnections || !options.AllowOrderSubmission || options.DryRun)
            throw new InvalidOperationException("DEMO_STRATEGY_EXTERNAL_ORDER_FLAGS_INVALID");
        if (string.IsNullOrWhiteSpace(options.FixOrderHost) || !options.FixOrderHost.Contains("demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEMO_STRATEGY_ORDER_HOST_NOT_DEMO");
        if (string.IsNullOrWhiteSpace(options.FixUsername) || string.IsNullOrWhiteSpace(options.FixPassword))
            throw new InvalidOperationException("DEMO_STRATEGY_FIX_CREDENTIALS_MISSING");
        if (!string.Equals(options.FixSenderCompId, options.FixUsername, StringComparison.Ordinal))
            throw new InvalidOperationException("DEMO_STRATEGY_TAG49_CONTINUITY_REQUIRED");
    }

    private static void ValidateStrategyRequest(LmaxConnectivityLabOptions options, LmaxDemoStrategyExecutionRequest request)
    {
        if (request.TargetKnownAtUtc.Offset != TimeSpan.Zero || request.TargetCloseUtc.Offset != TimeSpan.Zero || request.TargetKnownAtUtc > request.TargetCloseUtc)
            throw new InvalidOperationException("DEMO_STRATEGY_TARGET_TIME_INVALID");
        if (request.VenueQuantity <= 0m || request.VenueQuantity > options.MaxDemoOrderQuantity)
            throw new InvalidOperationException("DEMO_STRATEGY_QUANTITY_OUTSIDE_CONFIGURED_DEMO_LIMIT");
        if (request.PriceTickSize <= 0m) throw new InvalidOperationException("DEMO_STRATEGY_PRICE_TICK_INVALID");
        LmaxFixRecoveryCodec.ValidateClientOrderId(request.RootClientOrderId);
    }

    private static string ChildClOrdId(string root, int sequence, string suffix)
    {
        var prefix = new string(root.Where(char.IsLetterOrDigit).ToArray());
        if (prefix.Length > 14) prefix = prefix[..14];
        var value = $"{prefix}{suffix}{sequence:00}";
        return value.Length <= 20 ? value : value[..20];
    }

    private static LmaxConnectivityLabOptions CopyForMarketData(LmaxConnectivityLabOptions source)
        => new()
        {
            Enabled = source.Enabled,
            EnvironmentName = source.EnvironmentName,
            AllowExternalConnections = true,
            AllowOrderSubmission = false,
            AllowLiveTrading = false,
            DryRun = true,
            VenueName = source.VenueName,
            AccountCode = source.AccountCode,
            FixOrderHost = source.FixOrderHost,
            FixOrderPort = source.FixOrderPort,
            FixMarketDataHost = source.FixMarketDataHost,
            FixMarketDataPort = source.FixMarketDataPort,
            FixSenderCompId = source.FixSenderCompId,
            FixOrderTargetCompId = source.FixOrderTargetCompId,
            FixMarketDataTargetCompId = source.FixMarketDataTargetCompId,
            FixTargetCompId = source.FixTargetCompId,
            FixUsername = source.FixUsername,
            FixPassword = source.FixPassword,
            UseTls = source.UseTls,
            CheckCertificateRevocation = source.CheckCertificateRevocation,
            InstrumentSymbol = source.InstrumentSymbol,
            LmaxInstrumentId = source.LmaxInstrumentId,
            LmaxSlashSymbol = source.LmaxSlashSymbol,
            FixSecurityIdSource = source.FixSecurityIdSource,
            MarketDepth = source.MarketDepth,
            MarketDataRequestMode = source.MarketDataRequestMode,
            ConnectTimeoutSeconds = source.ConnectTimeoutSeconds,
            LogonTimeoutSeconds = source.LogonTimeoutSeconds,
            MarketDataMaxWaitSeconds = source.MarketDataMaxWaitSeconds,
            MarketDataMaxMessages = source.MarketDataMaxMessages,
            MarketDataSymbolEncodingMode = source.MarketDataSymbolEncodingMode,
            ShowFixMessages = source.ShowFixMessages,
            RequestTimeoutSeconds = source.RequestTimeoutSeconds,
            MaxDemoOrderQuantity = source.MaxDemoOrderQuantity,
            MaxDemoOrderNotionalUsd = source.MaxDemoOrderNotionalUsd
        };
}
