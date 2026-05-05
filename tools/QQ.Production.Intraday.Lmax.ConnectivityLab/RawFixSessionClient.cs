namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

public sealed class RawLmaxFixSessionClient(LmaxConnectivityLabSafetyValidator safety) : ILmaxFixSessionClient
{
    public LabCommandResult Validate(LmaxConnectivityLabOptions options, bool marketData)
    {
        var missing = MissingStructuralFixFields(options, marketData).ToList();
        var command = marketData ? "fix-market-data-smoke" : "fix-session-dry-run";
        if (missing.Count > 0)
        {
            return LabCommandResult.Skipped(command, $"Missing FIX config: {string.Join(", ", missing)}.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
        }

        return LabCommandResult.Ok(command, "FIX configuration is structurally complete. No socket connection was opened.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
    }

    public Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
        => Task.FromResult(Validate(options, marketData));

    public async Task<LabCommandResult> LogonSmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
    {
        var command = marketData ? "fix-marketdata-logon-smoke" : "fix-order-logon-smoke";
        var sessionType = marketData ? "MarketData" : "Order";
        var startedAt = DateTimeOffset.UtcNow;
        var issues = safety.ValidateForFixLogon(options, marketData).ToList();
        if (issues.Count > 0)
        {
            return new LabCommandResult(command, "Skipped", string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }

        var host = marketData ? options.FixMarketDataHost! : options.FixOrderHost!;
        var port = marketData ? options.FixMarketDataPort!.Value : options.FixOrderPort!.Value;
        var target = marketData ? (options.FixMarketDataTargetCompId ?? options.FixTargetCompId)! : (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds)));

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, timeout.Token);
            await using var stream = options.UseTls ? await CreateTlsStreamAsync(tcp, host, timeout.Token) : tcp.GetStream();

            var logon = LmaxFixMarketDataCodec.BuildMessage("A", 1, options.FixSenderCompId!, target, [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            await WriteAsciiAsync(stream, logon, timeout.Token);

            var response = await ReadFixResponseAsync(stream, timeout.Token);
            var loggedOn = LmaxFixMarketDataCodec.ContainsTag(response, "35", "A");
            var receivedLogout = LmaxFixMarketDataCodec.ContainsTag(response, "35", "5");
            var status = loggedOn ? "Ok" : "Failed";
            var message = loggedOn
                ? "FIX logon succeeded and logout was sent. No orders or subscriptions were submitted."
                : receivedLogout
                    ? "FIX session returned Logout before logon was confirmed."
                    : "FIX logon was not confirmed before timeout or session response.";

            if (loggedOn)
            {
                var logout = LmaxFixMarketDataCodec.BuildMessage("5", 2, options.FixSenderCompId!, target, [("58", "Connectivity lab logoff")]);
                await WriteAsciiAsync(stream, logout, CancellationToken.None);
            }

            return new LabCommandResult(command, status, message, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, true, loggedOn, startedAt, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            return new LabCommandResult(command, "Failed", "FIX logon smoke timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return new LabCommandResult(command, "Failed", $"FIX logon smoke failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }
    }

    public async Task<LmaxFixMarketDataSmokeResult> MarketDataSnapshotSmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var issues = safety.ValidateForFixLogon(options, marketData: true).ToList();
        var diagnostics = BuildSafeDiagnostics(options).ToList();
        if (issues.Count > 0)
        {
            var skipped = LmaxFixMarketDataSmokeResult.Skipped(string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
            return skipped with { Diagnostics = diagnostics };
        }

        var requestOptions = LmaxFixMarketDataRequestOptions.FromLabOptions(options);
        var requestModes = GetRequestModeAttempts(requestOptions.RequestMode).ToList();
        var encodings = GetEncodingAttempts(requestOptions.SymbolEncodingMode).ToList();
        var allAttempts = new List<string>();
        LmaxFixMarketDataSmokeResult? lastResult = null;
        foreach (var requestMode in requestModes)
        {
            foreach (var encoding in encodings)
            {
                var attemptOptions = requestOptions.WithRequestMode(requestMode).WithEncoding(encoding);
                var attemptLabel = $"RequestMode={requestMode};Encoding={encoding}";
                allAttempts.Add($"{attemptLabel}: started with clean FIX session");
                var result = await RunSingleMarketDataSnapshotAttemptAsync(options, attemptOptions, attemptLabel, startedAt, diagnostics, cancellationToken);
                allAttempts.AddRange(result.Attempts);
                lastResult = result with { Attempts = allAttempts.ToList() };
                if (result.Status == "Ok" || requestOptions.RequestMode != LmaxFixMarketDataRequestMode.Auto && requestOptions.SymbolEncodingMode != LmaxFixMarketDataSymbolEncodingMode.Auto)
                {
                    return lastResult;
                }
            }
        }

        return lastResult ?? LmaxFixMarketDataSmokeResult.Create("Failed", "No market data request attempts were made.", startedAt, false, false, false, false, false, false, false, false, null, null, null, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics, allAttempts);
    }

    public async Task<LmaxFixTradeCaptureSmokeResult> TradeCaptureSmokeAsync(LmaxConnectivityLabOptions options, LmaxFixTradeCaptureRequestOptions requestOptions, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var issues = safety.ValidateForFixLogon(options, marketData: false).ToList();
        var diagnostics = new List<string>
        {
            $"Host={options.FixOrderHost ?? "(not configured)"}",
            $"Port={options.FixOrderPort?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"TargetCompId={options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "(not configured)"}",
            $"SenderCompId={LmaxConnectivityLabOptions.Mask(options.FixSenderCompId)}",
            $"StartUtc={requestOptions.StartUtc:O}",
            $"EndUtc={requestOptions.EndUtc:O}",
            $"AccountConfigured={!string.IsNullOrWhiteSpace(requestOptions.Account)}",
            $"MaxReports={requestOptions.MaxReports}"
        };
        if (issues.Count > 0)
        {
            return LmaxFixTradeCaptureSmokeResult.Skipped(string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options)) with { Diagnostics = diagnostics };
        }

        var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var reports = new List<LmaxFixTradeCaptureReport>();
        var sequenceNumber = 1;
        var connected = false;
        var loggedOn = false;
        var requestSent = false;
        var ackReceived = false;
        var ackAccepted = false;
        string? ackRejectText = null;
        try
        {
            using var tcp = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                await tcp.ConnectAsync(options.FixOrderHost!, options.FixOrderPort!.Value, connectTimeout.Token);
                connected = true;
            }

            Stream rawStream;
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                rawStream = options.UseTls ? await CreateTlsStreamAsync(tcp, options.FixOrderHost!, connectTimeout.Token) : tcp.GetStream();
            }

            await using var stream = rawStream;
            var logon = LmaxFixMarketDataCodec.BuildMessage("A", sequenceNumber++, options.FixSenderCompId!, target, [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, logon, logonTimeout.Token);
                var logonResponse = await ReadFixResponseAsync(stream, logonTimeout.Token);
                loggedOn = LmaxFixMarketDataCodec.ContainsTag(logonResponse, "35", "A");
            }

            if (!loggedOn)
            {
                return new("fix-trade-capture-smoke", "Failed", connected, false, false, false, false, null, 0, false, [], startedAt, DateTimeOffset.UtcNow, "FIX trading logon was not confirmed; trade capture request was not sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
            }

            var tradeRequestId = $"QQTC-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            var request = LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest(options.FixSenderCompId!, target, sequenceNumber++, tradeRequestId, requestOptions);
            if (requestOptions.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(request)}");
            using (var wait = CreateTimeout(requestOptions.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, request, wait.Token);
                requestSent = true;
                while (!wait.IsCancellationRequested && reports.Count < requestOptions.MaxReports)
                {
                    var (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) break;
                    var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (requestOptions.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (msgType == "AQ")
                    {
                        var ack = LmaxFixRecoveryCodec.ParseTradeCaptureAck(message);
                        ackReceived = true;
                        ackAccepted = ack.Accepted;
                        ackRejectText = ack.Text;
                        if (!ackAccepted) break;
                    }
                    else if (msgType == "AE")
                    {
                        var report = LmaxFixRecoveryCodec.ParseTradeCaptureReport(message);
                        reports.Add(report);
                        if (report.LastReportRequested) break;
                    }
                    else if (msgType is "5" or "3")
                    {
                        ackRejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                        break;
                    }
                }
            }

            await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "TradeCapture");
            var lastRequested = reports.Any(x => x.LastReportRequested);
            var status = ackReceived && ackAccepted ? "Ok" : ackReceived ? "Failed" : "Failed";
            var messageText = ackReceived && ackAccepted
                ? reports.Count == 0 ? "Trade capture request accepted; no reports received before timeout. No data was persisted." : "Trade capture reports received. No data was persisted."
                : ackReceived ? "Trade capture request was rejected or not accepted." : "No TradeCaptureReportRequestAck was received before timeout.";
            return new("fix-trade-capture-smoke", status, connected, loggedOn, requestSent, ackReceived, ackAccepted, ackRejectText, reports.Count, lastRequested, reports, startedAt, DateTimeOffset.UtcNow, messageText, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (OperationCanceledException)
        {
            return new("fix-trade-capture-smoke", "Failed", connected, loggedOn, requestSent, ackReceived, ackAccepted, ackRejectText, reports.Count, reports.Any(x => x.LastReportRequested), reports, startedAt, DateTimeOffset.UtcNow, "Trade capture smoke timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return new("fix-trade-capture-smoke", "Failed", connected, loggedOn, requestSent, ackReceived, ackAccepted, ackRejectText, reports.Count, reports.Any(x => x.LastReportRequested), reports, startedAt, DateTimeOffset.UtcNow, $"Trade capture smoke failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
    }

    private static async Task<LmaxFixMarketDataSmokeResult> RunSingleMarketDataSnapshotAttemptAsync(
        LmaxConnectivityLabOptions options,
        LmaxFixMarketDataRequestOptions requestOptions,
        string attemptLabel,
        DateTimeOffset startedAt,
        IReadOnlyList<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var host = options.FixMarketDataHost!;
        var port = options.FixMarketDataPort!.Value;
        var target = (options.FixMarketDataTargetCompId ?? options.FixTargetCompId)!;
        var messages = new List<string>();
        var entries = new List<LmaxFixMarketDataEntry>();
        var mdReqId = $"QQMD-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var attempts = new List<string> { $"{attemptLabel}: MDReqID={mdReqId}" };
        var tcpConnected = false;
        var tlsHandshakeCompleted = false;
        var fixLogonSent = false;
        var fixLoggedOn = false;
        var marketDataRequestSent = false;
        var marketDataSnapshotReceived = false;
        var marketDataRejectReceived = false;
        var logoutSent = false;
        string? rejectReason = null;
        string? rejectText = null;
        string? lastMsgType = null;
        Stream? stream = null;
        var sequenceNumber = 1;

        try
        {
            using var tcp = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                await tcp.ConnectAsync(host, port, connectTimeout.Token);
                tcpConnected = true;
            }

            Stream rawStream;
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                rawStream = options.UseTls ? await CreateTlsStreamAsync(tcp, host, connectTimeout.Token) : tcp.GetStream();
                tlsHandshakeCompleted = options.UseTls || rawStream is not null;
            }

            await using var disposableStream = rawStream;
            stream = disposableStream;
            Stream activeStream = disposableStream ?? throw new IOException("FIX stream was not created.");

            var logon = LmaxFixMarketDataCodec.BuildMessage("A", sequenceNumber++, options.FixSenderCompId!, target, [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
            {
                await WriteAsciiAsync(activeStream, logon, logonTimeout.Token);
                fixLogonSent = true;
                var logonResponse = await ReadFixResponseAsync(activeStream, logonTimeout.Token);
                lastMsgType = LmaxFixMarketDataCodec.GetMsgType(logonResponse);
                messages.Add(logonResponse);
                fixLoggedOn = LmaxFixMarketDataCodec.ContainsTag(logonResponse, "35", "A");
            }

            if (!fixLoggedOn)
            {
                return LmaxFixMarketDataSmokeResult.Create("Failed", "FIX market data logon was not confirmed; market data request was not sent.", startedAt, tcpConnected, tlsHandshakeCompleted, fixLogonSent, fixLoggedOn, false, false, false, logoutSent, null, null, lastMsgType, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics, attempts, messageCount: messages.Count);
            }

            var request = LmaxFixMarketDataCodec.BuildMarketDataRequest(options.FixSenderCompId!, target, sequenceNumber++, mdReqId, requestOptions);
            attempts.AddRange(LmaxFixMarketDataCodec.DescribeMarketDataRequest(request).Select(x => $"{attemptLabel}: {x}"));
            if (requestOptions.ShowFixMessages)
            {
                attempts.Add($"{attemptLabel}: OUT {LmaxFixMarketDataCodec.SanitizeMessage(request)}");
            }

            using (var marketDataTimeout = CreateTimeout(requestOptions.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(activeStream, request, marketDataTimeout.Token);
                attempts.Add($"{attemptLabel}: request bytes written and flushed");
                marketDataRequestSent = true;
                while (messages.Count < requestOptions.MaxMessages + 1 && !marketDataTimeout.IsCancellationRequested)
                {
                    var readResult = await ReadMarketDataResponseAsync(activeStream, options, target, sequenceNumber, marketDataTimeout.Token);
                    sequenceNumber = readResult.NextSequenceNumber;
                    var message = readResult.Message;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        break;
                    }

                    messages.Add(message);
                    lastMsgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    attempts.Add($"{attemptLabel}: received MsgType={lastMsgType ?? "(unknown)"}");
                    if (requestOptions.ShowFixMessages)
                    {
                        attempts.Add($"{attemptLabel}: IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    }

                    if (LmaxFixMarketDataCodec.ContainsTag(message, "35", "W") || LmaxFixMarketDataCodec.ContainsTag(message, "35", "X"))
                    {
                        entries.AddRange(LmaxFixMarketDataCodec.ParseMarketDataEntries(message));
                        marketDataSnapshotReceived = true;
                        break;
                    }

                    if (LmaxFixMarketDataCodec.ContainsTag(message, "35", "Y"))
                    {
                        var reject = LmaxFixMarketDataCodec.ParseReject(message);
                        marketDataRejectReceived = true;
                        rejectReason = reject.Reason;
                        rejectText = reject.Text;
                        attempts.Add($"{attemptLabel}: rejected reason={rejectReason} text={rejectText}");
                        break;
                    }

                    if (LmaxFixMarketDataCodec.ContainsTag(message, "35", "3"))
                    {
                        rejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                        attempts.Add($"{attemptLabel}: session-level reject text={rejectText}");
                        break;
                    }

                    if (LmaxFixMarketDataCodec.ContainsTag(message, "35", "5"))
                    {
                        rejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                        attempts.Add($"{attemptLabel}: logout received text={rejectText}");
                        break;
                    }
                }
            }

            if (requestOptions.RequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates)
            {
                var unsubscribe = LmaxFixMarketDataCodec.BuildMarketDataRequest(options.FixSenderCompId!, target, sequenceNumber++, mdReqId, requestOptions, unsubscribe: true);
                if (requestOptions.ShowFixMessages)
                {
                    attempts.Add($"{attemptLabel}: OUT {LmaxFixMarketDataCodec.SanitizeMessage(unsubscribe)}");
                }

                await WriteAsciiAsync(activeStream, unsubscribe, CancellationToken.None);
                attempts.Add($"{attemptLabel}: unsubscribe sent");
            }

            await TrySendLogoutAsync(activeStream, options, target, sequenceNumber, attempts, attemptLabel);
            logoutSent = true;
            var (bestBid, bestAsk, mid) = LmaxFixMarketDataCodec.ComputeTopOfBook(entries);
            var status = entries.Count > 0 ? "Ok" : "Failed";
            var text = entries.Count > 0
                ? "Received market data entries. No data was persisted."
                : marketDataRejectReceived
                    ? "LMAX rejected the MarketDataRequest."
                    : "No market data snapshot or incremental entries were received before timeout.";
            return LmaxFixMarketDataSmokeResult.Create(status, text, startedAt, tcpConnected, tlsHandshakeCompleted, fixLogonSent, fixLoggedOn, marketDataRequestSent, marketDataSnapshotReceived, marketDataRejectReceived, logoutSent, rejectReason, rejectText, lastMsgType, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics, attempts, entries, bestBid, bestAsk, mid, messages.Count);
        }
        catch (OperationCanceledException)
        {
            if (fixLoggedOn && stream is not null)
            {
                logoutSent = await TrySendLogoutAsync(stream, options, target, sequenceNumber, attempts, attemptLabel);
            }

            var phase = !tcpConnected ? "TCP connect" : !tlsHandshakeCompleted && options.UseTls ? "TLS handshake" : !fixLoggedOn ? "FIX logon" : marketDataRequestSent ? "market data response" : "market data request";
            return LmaxFixMarketDataSmokeResult.Create("Failed", $"Market data snapshot smoke timed out during {phase}.", startedAt, tcpConnected, tlsHandshakeCompleted, fixLogonSent, fixLoggedOn, marketDataRequestSent, marketDataSnapshotReceived, marketDataRejectReceived, logoutSent, rejectReason, rejectText, lastMsgType, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics, attempts, entries, messageCount: messages.Count);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return LmaxFixMarketDataSmokeResult.Create("Failed", $"Market data snapshot smoke failed: {ex.GetType().Name}: {ex.Message}", startedAt, tcpConnected, tlsHandshakeCompleted, fixLogonSent, fixLoggedOn, marketDataRequestSent, marketDataSnapshotReceived, marketDataRejectReceived, logoutSent, rejectReason, rejectText, lastMsgType, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics, attempts, entries, messageCount: messages.Count);
        }
    }

    private static async Task<Stream> CreateTlsStreamAsync(TcpClient tcp, string host, CancellationToken cancellationToken)
    {
        var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(host, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: true).WaitAsync(cancellationToken);
        return ssl;
    }

    private static async Task WriteAsciiAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadFixResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
            var text = builder.ToString();
            if (LmaxFixMarketDataCodec.ContainsTag(text, "35", "A") ||
                LmaxFixMarketDataCodec.ContainsTag(text, "35", "5") ||
                LmaxFixMarketDataCodec.ContainsTag(text, "35", "W") ||
                LmaxFixMarketDataCodec.ContainsTag(text, "35", "X") ||
                LmaxFixMarketDataCodec.ContainsTag(text, "35", "Y"))
            {
                return text;
            }
        }

        return builder.ToString();
    }

    private static async Task<(string Message, int NextSequenceNumber)> ReadMarketDataResponseAsync(
        Stream stream,
        LmaxConnectivityLabOptions options,
        string target,
        int sequenceNumber,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadAnyFixMessageAsync(stream, cancellationToken);
            var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
            if (msgType == "1")
            {
                var testReqId = LmaxFixMarketDataCodec.GetTag(message, "112");
                IReadOnlyList<(string Tag, string Value)> heartbeatFields = string.IsNullOrWhiteSpace(testReqId)
                    ? []
                    : [("112", testReqId)];
                var heartbeat = LmaxFixMarketDataCodec.BuildMessage("0", sequenceNumber++, options.FixSenderCompId!, target, heartbeatFields);
                await WriteAsciiAsync(stream, heartbeat, cancellationToken);
                continue;
            }

            if (msgType == "0")
            {
                continue;
            }

            return (message, sequenceNumber);
        }

        return (string.Empty, sequenceNumber);
    }

    private static async Task<string> ReadAnyFixMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
            var text = builder.ToString();
            if (text.Contains($"{LmaxFixMarketDataCodec.Soh}10=", StringComparison.Ordinal))
            {
                return text;
            }
        }

        return builder.ToString();
    }

    private static async Task<bool> TrySendLogoutAsync(Stream stream, LmaxConnectivityLabOptions options, string target, int sequenceNumber, ICollection<string> attempts, string attemptLabel)
    {
        try
        {
            var logout = LmaxFixMarketDataCodec.BuildMessage("5", sequenceNumber, options.FixSenderCompId!, target, [("58", "Connectivity lab FIX logoff")]);
            if (options.ShowFixMessages)
            {
                attempts.Add($"{attemptLabel}: OUT {LmaxFixMarketDataCodec.SanitizeMessage(logout)}");
            }

            await WriteAsciiAsync(stream, logout, CancellationToken.None);
            attempts.Add($"{attemptLabel}: logout sent");
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or AuthenticationException)
        {
            attempts.Add($"{attemptLabel}: logout attempt failed {ex.GetType().Name}");
            return false;
        }
    }

    private static async Task SendMarketDataCleanupAsync(Stream stream, LmaxConnectivityLabOptions options, string target, string mdReqId, int sequenceNumber, LmaxFixMarketDataRequestOptions requestOptions, bool unsubscribe)
    {
        if (unsubscribe)
        {
            var unsubscribeMessage = LmaxFixMarketDataCodec.BuildMarketDataRequest(options.FixSenderCompId!, target, sequenceNumber, mdReqId, requestOptions, unsubscribe: true);
            await WriteAsciiAsync(stream, unsubscribeMessage, CancellationToken.None);
            sequenceNumber++;
        }

        var logout = LmaxFixMarketDataCodec.BuildMessage("5", sequenceNumber, options.FixSenderCompId!, target, [("58", "Connectivity lab market data logoff")]);
        await WriteAsciiAsync(stream, logout, CancellationToken.None);
    }

    private static CancellationTokenSource CreateTimeout(int seconds, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        return source;
    }

    private static IEnumerable<LmaxFixMarketDataSymbolEncodingMode> GetEncodingAttempts(LmaxFixMarketDataSymbolEncodingMode requested)
        => requested == LmaxFixMarketDataSymbolEncodingMode.Auto
            ? [
                LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource,
                LmaxFixMarketDataSymbolEncodingMode.SecurityIdNoIdSource,
                LmaxFixMarketDataSymbolEncodingMode.SlashSymbol,
                LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource,
                LmaxFixMarketDataSymbolEncodingMode.SecurityId,
                LmaxFixMarketDataSymbolEncodingMode.InternalSymbol
            ]
            : [requested];

    private static IEnumerable<LmaxFixMarketDataRequestMode> GetRequestModeAttempts(LmaxFixMarketDataRequestMode requested)
        => requested == LmaxFixMarketDataRequestMode.Auto
            ? [LmaxFixMarketDataRequestMode.SnapshotPlusUpdates, LmaxFixMarketDataRequestMode.SnapshotOnly]
            : [requested];

    private static IEnumerable<string> BuildSafeDiagnostics(LmaxConnectivityLabOptions options)
    {
        yield return $"Host={options.FixMarketDataHost ?? "(not configured)"}";
        yield return $"Port={options.FixMarketDataPort?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}";
        yield return $"TargetCompId={options.FixMarketDataTargetCompId ?? options.FixTargetCompId ?? "(not configured)"}";
        yield return $"SenderCompId={LmaxConnectivityLabOptions.Mask(options.FixSenderCompId)}";
        yield return $"EnvironmentName={options.EnvironmentName}";
        yield return $"AllowExternalConnections={options.AllowExternalConnections}";
        yield return $"UseTls={options.UseTls}";
        yield return $"ConnectTimeoutSeconds={options.ConnectTimeoutSeconds}";
        yield return $"LogonTimeoutSeconds={options.LogonTimeoutSeconds}";
        yield return $"MarketDataWaitSeconds={options.MarketDataMaxWaitSeconds}";
        yield return $"RequestMode={options.MarketDataRequestMode}";
        yield return $"MarketDepth={options.MarketDepth}";
        yield return $"SymbolEncodingMode={options.MarketDataSymbolEncodingMode}";
        yield return $"InstrumentSymbol={options.InstrumentSymbol}";
        yield return $"LmaxInstrumentId={options.LmaxInstrumentId}";
        yield return $"LmaxSlashSymbol={options.LmaxSlashSymbol}";
    }

    private static IEnumerable<string> MissingStructuralFixFields(LmaxConnectivityLabOptions options, bool marketData)
    {
        if (marketData)
        {
            if (string.IsNullOrWhiteSpace(options.FixMarketDataHost)) yield return nameof(options.FixMarketDataHost);
            if (options.FixMarketDataPort is null) yield return nameof(options.FixMarketDataPort);
            if (string.IsNullOrWhiteSpace(options.FixMarketDataTargetCompId ?? options.FixTargetCompId)) yield return nameof(options.FixMarketDataTargetCompId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.FixOrderHost)) yield return nameof(options.FixOrderHost);
            if (options.FixOrderPort is null) yield return nameof(options.FixOrderPort);
            if (string.IsNullOrWhiteSpace(options.FixOrderTargetCompId ?? options.FixTargetCompId)) yield return nameof(options.FixOrderTargetCompId);
        }

        if (string.IsNullOrWhiteSpace(options.FixSenderCompId)) yield return nameof(options.FixSenderCompId);
        if (string.IsNullOrWhiteSpace(options.FixUsername)) yield return nameof(options.FixUsername);
    }
}
