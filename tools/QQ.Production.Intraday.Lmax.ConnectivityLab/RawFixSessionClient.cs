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
        var requestRejected = false;
        string? ackRejectText = null;
        string? rejectMsgType = null;
        string? rejectRefTagId = null;
        string? rejectRefMsgType = null;
        string? rejectReasonCode = null;
        string? rejectText = null;
        string? lastReceivedMsgType = null;
        int? expectedTradeReportCount = null;
        var noMoreReports = false;
        var logoutSent = false;
        var timedOutWaitingForReports = false;
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
                return new("fix-trade-capture-smoke", "Failed", connected, false, false, false, false, false, null, null, null, null, null, null, null, null, false, false, 0, false, [], startedAt, DateTimeOffset.UtcNow, "FIX trading logon was not confirmed; trade capture request was not sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
            }

            var tradeRequestId = LmaxFixRecoveryCodec.GenerateTradeRequestId(DateTimeOffset.UtcNow, sequenceNumber);
            try
            {
                LmaxFixRecoveryCodec.ValidateTradeRequestId(tradeRequestId);
            }
            catch (ArgumentException ex)
            {
                return new("fix-trade-capture-smoke", "Failed", connected, loggedOn, false, false, false, false, null, null, null, null, null, null, null, null, false, false, 0, false, [], startedAt, DateTimeOffset.UtcNow, $"Trade capture request was not sent because generated TradeRequestID is invalid: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
            }

            var request = LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest(options.FixSenderCompId!, target, sequenceNumber++, tradeRequestId, requestOptions);
            diagnostics.Add($"TradeRequestID={tradeRequestId}");
            if (requestOptions.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(request)}");
            using (var wait = CreateTimeout(requestOptions.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, request, wait.Token);
                requestSent = true;
                while (!wait.IsCancellationRequested && reports.Count < requestOptions.MaxReports)
                {
                    string message;
                    int nextSequence;
                    try
                    {
                        (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        timedOutWaitingForReports = true;
                        break;
                    }

                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) break;
                    var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    lastReceivedMsgType = msgType;
                    if (requestOptions.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (msgType == "AQ")
                    {
                        var ack = LmaxFixRecoveryCodec.ParseTradeCaptureAck(message);
                        ackReceived = true;
                        ackAccepted = ack.Accepted;
                        ackRejectText = ack.Text;
                        expectedTradeReportCount = ack.TotNumTradeReports;
                        diagnostics.Add($"TradeCaptureAck: TradeRequestID={ack.RequestId ?? "(missing)"} TotNumTradeReports={expectedTradeReportCount?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"} Result={ack.Result ?? "(missing)"} Status={ack.Status ?? "(missing)"}");
                        if (ackAccepted && expectedTradeReportCount == 0)
                        {
                            noMoreReports = true;
                            break;
                        }

                        if (!ackAccepted) break;
                    }
                    else if (msgType == "AE")
                    {
                        var normalized = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, options);
                        reports.Add(normalized.Report);
                        foreach (var warning in normalized.Warnings)
                        {
                            diagnostics.Add($"TradeCaptureReport warning: {warning}");
                        }

                        if (normalized.Report.LastReportRequested || expectedTradeReportCount is not null && reports.Count >= expectedTradeReportCount.Value)
                        {
                            noMoreReports = true;
                            break;
                        }
                    }
                    else if (msgType == "3")
                    {
                        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                        rejectMsgType = "3";
                        rejectRefTagId = reject.RefTagId;
                        rejectRefMsgType = reject.RefMsgType;
                        rejectReasonCode = reject.SessionRejectReason;
                        rejectText = reject.Text;
                        ackRejectText = reject.Text;
                        requestRejected = string.Equals(reject.RefMsgType, "AD", StringComparison.Ordinal);
                        break;
                    }
                    else if (msgType == "5")
                    {
                        ackRejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                        break;
                    }
                }
            }

            logoutSent = await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "TradeCapture");
            var lastRequested = reports.Any(x => x.LastReportRequested);
            var status = ResolveTradeCaptureStatus(ackReceived, ackAccepted, requestRejected, expectedTradeReportCount, reports.Count, timedOutWaitingForReports);
            var messageText = ResolveTradeCaptureMessage(ackReceived, ackAccepted, requestRejected, rejectText, expectedTradeReportCount, reports.Count, timedOutWaitingForReports);
            return new("fix-trade-capture-smoke", status, connected, loggedOn, requestSent, ackReceived, ackAccepted, requestRejected, ackRejectText, rejectMsgType, rejectRefTagId, rejectRefMsgType, rejectReasonCode, rejectText, lastReceivedMsgType, expectedTradeReportCount, noMoreReports, logoutSent, reports.Count, lastRequested || noMoreReports, reports, startedAt, DateTimeOffset.UtcNow, messageText, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (OperationCanceledException)
        {
            return new("fix-trade-capture-smoke", "Failed", connected, loggedOn, requestSent, ackReceived, ackAccepted, requestRejected, ackRejectText, rejectMsgType, rejectRefTagId, rejectRefMsgType, rejectReasonCode, rejectText, lastReceivedMsgType, expectedTradeReportCount, noMoreReports, logoutSent, reports.Count, reports.Any(x => x.LastReportRequested), reports, startedAt, DateTimeOffset.UtcNow, "Trade capture smoke timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return new("fix-trade-capture-smoke", "Failed", connected, loggedOn, requestSent, ackReceived, ackAccepted, requestRejected, ackRejectText, rejectMsgType, rejectRefTagId, rejectRefMsgType, rejectReasonCode, rejectText, lastReceivedMsgType, expectedTradeReportCount, noMoreReports, logoutSent, reports.Count, reports.Any(x => x.LastReportRequested), reports, startedAt, DateTimeOffset.UtcNow, $"Trade capture smoke failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
    }

    public async Task<LmaxFixDemoOrderLifecycleResult> DemoOrderLifecycleAsync(LmaxConnectivityLabOptions options, LmaxFixDemoOrderRequest request, bool explicitConfirmation, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<string>
        {
            $"Host={options.FixOrderHost ?? "(not configured)"}",
            $"Port={options.FixOrderPort?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"TargetCompId={options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "(not configured)"}",
            $"SenderCompId={LmaxConnectivityLabOptions.Mask(options.FixSenderCompId)}",
            $"InstrumentSymbol={request.InstrumentSymbol}",
            $"LmaxInstrumentId={request.LmaxInstrumentId}",
            $"Side={request.Side}",
            $"OrderType={request.OrderType}",
            $"TimeInForce={request.TimeInForce}",
            $"VenueQuantity={request.VenueQuantity.ToString(CultureInfo.InvariantCulture)}",
            $"LimitPrice={request.LimitPrice?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"MaxNotionalUsd={request.MaxNotionalUsd?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"AccountConfigured={!string.IsNullOrWhiteSpace(request.Account)}"
        };
        var decisions = safety.ValidateForDemoOrderLifecycle(options, request, explicitConfirmation).ToList();
        var clOrdId = request.ClientOrderId ?? LmaxFixRecoveryCodec.GenerateClientOrderId(DateTimeOffset.UtcNow, 2);

        if (request.DryRun)
        {
            var sender = string.IsNullOrWhiteSpace(options.FixSenderCompId) ? "DRYRUN-SENDER" : options.FixSenderCompId!;
            var target = options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "LMXBD";
            try
            {
                var dryRunMessage = LmaxFixRecoveryCodec.BuildNewOrderSingle(sender, target, 2, request, clOrdId, options.FixSecurityIdSource);
                diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(dryRunMessage)}");
                return new("fix-demo-order-lifecycle", "Ok", false, false, false, false, false, false, null, null, null, null, null, null, false, clOrdId, null, [], startedAt, DateTimeOffset.UtcNow, "Built demo NewOrderSingle dry-run. No network call was made and no order was submitted.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
            }
            catch (ArgumentException ex)
            {
                return new("fix-demo-order-lifecycle", "Failed", false, false, false, false, false, false, null, null, null, null, null, null, false, clOrdId, null, [], startedAt, DateTimeOffset.UtcNow, $"Demo order dry-run could not build NewOrderSingle: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
            }
        }

        if (decisions.Any(x => !x.Passed))
        {
            return LmaxFixDemoOrderLifecycleResult.Skipped("Demo order lifecycle safety gates did not pass. No order was sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions) with { Diagnostics = diagnostics, ClientOrderId = clOrdId };
        }

        var targetCompId = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var reports = new List<LmaxFixExecutionReport>();
        var sequenceNumber = 1;
        var connected = false;
        var loggedOn = false;
        var orderSent = false;
        var terminal = false;
        var logoutSent = false;
        string? lastMsgType = null;

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
            var logon = LmaxFixMarketDataCodec.BuildMessage("A", sequenceNumber++, options.FixSenderCompId!, targetCompId, [
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
                lastMsgType = LmaxFixMarketDataCodec.GetMsgType(logonResponse);
            }

            if (!loggedOn)
            {
                return new("fix-demo-order-lifecycle", "Failed", connected, false, false, false, false, false, null, null, null, null, null, null, false, clOrdId, lastMsgType, [], startedAt, DateTimeOffset.UtcNow, "FIX trading logon was not confirmed; demo order was not sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
            }

            var newOrder = LmaxFixRecoveryCodec.BuildNewOrderSingle(options.FixSenderCompId!, targetCompId, sequenceNumber++, request, clOrdId, options.FixSecurityIdSource);
            if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(newOrder)}");
            using (var wait = CreateTimeout(request.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, newOrder, wait.Token);
                orderSent = true;
                while (!wait.IsCancellationRequested)
                {
                    string message;
                    int nextSequence;
                    try
                    {
                        (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, targetCompId, sequenceNumber, wait.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) break;
                    lastMsgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (lastMsgType == "8")
                    {
                        var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options);
                        reports.Add(normalized.Report);
                        foreach (var warning in normalized.Warnings)
                        {
                            diagnostics.Add($"ExecutionReport warning: {warning}");
                        }

                        if (IsTerminalExecutionReport(normalized.Report))
                        {
                            terminal = true;
                            break;
                        }
                    }
                    else if (lastMsgType == "3")
                    {
                        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                        diagnostics.Add($"Received MsgType=3 RefTagID={reject.RefTagId ?? "(missing)"} RefMsgType={reject.RefMsgType ?? "(missing)"} Reason={reject.SessionRejectReason ?? "(missing)"} Text={reject.Text ?? "(none)"}");
                        if (string.Equals(reject.RefMsgType, "D", StringComparison.Ordinal))
                        {
                            logoutSent = await TrySendLogoutAsync(stream, options, targetCompId, sequenceNumber, diagnostics, "DemoOrderLifecycle");
                            return new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, reports.Count > 0, terminal, true, "3", reject.RefTagId, reject.RefMsgType, reject.SessionRejectReason, reject.Text, "ProtocolRejected", logoutSent, clOrdId, lastMsgType, reports, startedAt, DateTimeOffset.UtcNow, $"NewOrderSingle was rejected at FIX session level: {reject.Text ?? "(no reject text)"}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
                        }

                        break;
                    }
                    else if (lastMsgType == "5")
                    {
                        diagnostics.Add($"Received MsgType={lastMsgType} Text={LmaxFixMarketDataCodec.GetTag(message, "58") ?? "(none)"}");
                        break;
                    }
                }
            }

            logoutSent = await TrySendLogoutAsync(stream, options, targetCompId, sequenceNumber, diagnostics, "DemoOrderLifecycle");
            var status = reports.Count > 0 ? "Ok" : "Failed";
            var messageText = terminal
                ? "Demo order lifecycle received terminal ExecutionReport. No data was persisted."
                : reports.Count > 0
                    ? "Demo order lifecycle received ExecutionReport messages but no terminal state before timeout. No data was persisted."
                    : "Demo order lifecycle sent order but received no ExecutionReport before timeout.";
            return new("fix-demo-order-lifecycle", status, connected, loggedOn, orderSent, reports.Count > 0, terminal, false, null, null, null, null, null, terminal ? "TerminalExecutionReport" : null, logoutSent, clOrdId, lastMsgType, reports, startedAt, DateTimeOffset.UtcNow, messageText, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
        }
        catch (OperationCanceledException)
        {
            return new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, reports.Count > 0, terminal, false, null, null, null, null, null, null, logoutSent, clOrdId, lastMsgType, reports, startedAt, DateTimeOffset.UtcNow, "Demo order lifecycle timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or ArgumentException)
        {
            return new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, reports.Count > 0, terminal, false, null, null, null, null, null, null, logoutSent, clOrdId, lastMsgType, reports, startedAt, DateTimeOffset.UtcNow, $"Demo order lifecycle failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
        }
    }

    private static bool IsTerminalExecutionReport(LmaxFixExecutionReport report)
        => report.ExecType is LmaxFixExecutionReportType.Rejected or LmaxFixExecutionReportType.Canceled or LmaxFixExecutionReportType.Expired
           || report.OrdStatus is LmaxFixOrderStatus.Filled or LmaxFixOrderStatus.Canceled or LmaxFixOrderStatus.Rejected or LmaxFixOrderStatus.Expired;

    public async Task<LmaxFixOrderStatusSmokeResult> OrderStatusSmokeAsync(LmaxConnectivityLabOptions options, LmaxFixOrderStatusSmokeRequest request, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<string>
        {
            $"Host={options.FixOrderHost ?? "(not configured)"}",
            $"Port={options.FixOrderPort?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"TargetCompId={options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "(not configured)"}",
            $"SenderCompId={LmaxConnectivityLabOptions.Mask(options.FixSenderCompId)}",
            $"ClOrdID={request.ClOrdId ?? "(missing)"}",
            $"SecurityID={request.SecurityId ?? "(not configured)"}",
            $"AccountConfigured={!string.IsNullOrWhiteSpace(request.Account)}"
        };

        if (string.IsNullOrWhiteSpace(request.ClOrdId))
        {
            return LmaxFixOrderStatusSmokeResult.Skipped("ClOrdID is required for order status smoke. No network call was made.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), request.ClOrdId, diagnostics);
        }

        var issues = safety.ValidateForFixLogon(options, marketData: false).ToList();
        if (issues.Count > 0)
        {
            return LmaxFixOrderStatusSmokeResult.Skipped(string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), request.ClOrdId, diagnostics);
        }

        var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var sequenceNumber = 1;
        var connected = false;
        var loggedOn = false;
        var requestSent = false;
        var logoutSent = false;
        var requestRejected = false;
        string? rejectRefTagId = null;
        string? rejectRefMsgType = null;
        string? rejectText = null;
        var reports = new List<LmaxFixExecutionReport>();

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
                return new("fix-order-status-smoke", "Failed", connected, false, false, false, false, null, null, null, request.ClOrdId, null, null, [], startedAt, DateTimeOffset.UtcNow, false, "FIX trading logon was not confirmed; OrderStatusRequest was not sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
            }

            var securityIdSource = !string.IsNullOrWhiteSpace(request.SecurityId) && string.IsNullOrWhiteSpace(request.SecurityIdSource)
                ? options.FixSecurityIdSource
                : request.SecurityIdSource;
            var orderStatusRequest = LmaxFixRecoveryCodec.BuildOrderStatusRequest(
                options.FixSenderCompId!,
                target,
                sequenceNumber++,
                request.ClOrdId!,
                request.Account,
                request.SecurityId,
                securityIdSource,
                request.Side,
                request.OrdStatusReqId);
            if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(orderStatusRequest)}");
            using (var wait = CreateTimeout(request.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, orderStatusRequest, wait.Token);
                requestSent = true;
                while (!wait.IsCancellationRequested)
                {
                    string message;
                    int nextSequence;
                    try
                    {
                        (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) break;
                    var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (msgType == "8")
                    {
                        var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options);
                        reports.Add(normalized.Report);
                        foreach (var warning in normalized.Warnings)
                        {
                            diagnostics.Add($"ExecutionReport warning: {warning}");
                        }

                        break;
                    }

                    if (msgType == "3")
                    {
                        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                        requestRejected = true;
                        rejectRefTagId = reject.RefTagId;
                        rejectRefMsgType = reject.RefMsgType;
                        rejectText = reject.Text;
                        break;
                    }

                    if (msgType == "5")
                    {
                        rejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                        break;
                    }
                }
            }

            logoutSent = await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "OrderStatus");
            if (reports.Count > 0)
            {
                var latest = reports[^1];
                return new(
                    "fix-order-status-smoke",
                    "Ok",
                    connected,
                    loggedOn,
                    requestSent,
                    true,
                    false,
                    null,
                    null,
                    null,
                    request.ClOrdId,
                    latest.OrderId,
                    latest.OrdStatus.ToString(),
                    reports,
                    startedAt,
                    DateTimeOffset.UtcNow,
                    logoutSent,
                    $"Received ExecutionReport for ClOrdID={request.ClOrdId}; OrdStatus={latest.OrdStatus}. No data was persisted.",
                    LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options),
                    diagnostics);
            }

            if (requestRejected)
            {
                return new("fix-order-status-smoke", "Failed", connected, loggedOn, requestSent, false, true, rejectRefTagId, rejectRefMsgType, rejectText, request.ClOrdId, null, null, [], startedAt, DateTimeOffset.UtcNow, logoutSent, $"OrderStatusRequest was rejected at FIX session level: {rejectText ?? "(no reject text)"}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
            }

            return new("fix-order-status-smoke", "Failed", connected, loggedOn, requestSent, false, false, null, null, rejectText, request.ClOrdId, null, null, [], startedAt, DateTimeOffset.UtcNow, logoutSent, "OrderStatusRequest timed out before ExecutionReport or session reject was received.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (OperationCanceledException)
        {
            return new("fix-order-status-smoke", "Failed", connected, loggedOn, requestSent, reports.Count > 0, requestRejected, rejectRefTagId, rejectRefMsgType, rejectText, request.ClOrdId, reports.LastOrDefault()?.OrderId, reports.LastOrDefault()?.OrdStatus.ToString(), reports, startedAt, DateTimeOffset.UtcNow, logoutSent, "OrderStatusRequest smoke timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return new("fix-order-status-smoke", "Failed", connected, loggedOn, requestSent, reports.Count > 0, requestRejected, rejectRefTagId, rejectRefMsgType, rejectText, request.ClOrdId, reports.LastOrDefault()?.OrderId, reports.LastOrDefault()?.OrdStatus.ToString(), reports, startedAt, DateTimeOffset.UtcNow, logoutSent, $"OrderStatusRequest smoke failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        }
    }

    public async Task<LmaxFixLifecycleEvidenceResult> DemoLifecycleEvidenceAsync(LmaxConnectivityLabOptions options, LmaxFixDemoOrderRequest request, LmaxFixTradeCaptureRequestOptions tradeCaptureRequest, bool explicitConfirmation, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<string>
        {
            "Lifecycle evidence report is lab-only.",
            "No live data is persisted into the main DB.",
            "API/Worker integration is not used."
        };

        if (request.DryRun)
        {
            var dryRunOrder = await DemoOrderLifecycleAsync(options, request, explicitConfirmation, cancellationToken);
            diagnostics.AddRange(dryRunOrder.Diagnostics);
            diagnostics.Add("Dry-run evidence stopped before network I/O. No order was submitted.");
            return BuildLifecycleEvidenceResult(startedAt, options, request, dryRunOrder, null, null, diagnostics);
        }

        return await DemoLifecycleEvidenceOnSingleSessionAsync(options, request, tradeCaptureRequest, explicitConfirmation, startedAt, diagnostics, cancellationToken);
    }

    private async Task<LmaxFixLifecycleEvidenceResult> DemoLifecycleEvidenceOnSingleSessionAsync(
        LmaxConnectivityLabOptions options,
        LmaxFixDemoOrderRequest request,
        LmaxFixTradeCaptureRequestOptions tradeCaptureRequest,
        bool explicitConfirmation,
        DateTimeOffset startedAt,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var decisions = safety.ValidateForDemoOrderLifecycle(options, request, explicitConfirmation).ToList();
        var clOrdId = request.ClientOrderId ?? LmaxFixRecoveryCodec.GenerateClientOrderId(DateTimeOffset.UtcNow, 2);
        diagnostics.AddRange([
            $"Host={options.FixOrderHost ?? "(not configured)"}",
            $"Port={options.FixOrderPort?.ToString(CultureInfo.InvariantCulture) ?? "(not configured)"}",
            $"TargetCompId={options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "(not configured)"}",
            $"SenderCompId={LmaxConnectivityLabOptions.Mask(options.FixSenderCompId)}",
            $"ClientOrderId={clOrdId}",
            "SameSessionRecovery=True",
            "RecoveryLogonAttempts=0"
        ]);

        if (decisions.Any(x => !x.Passed))
        {
            var skipped = LmaxFixDemoOrderLifecycleResult.Skipped("Demo lifecycle evidence safety gates did not pass. No order was sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions) with { Diagnostics = diagnostics, ClientOrderId = clOrdId };
            return BuildLifecycleEvidenceResult(startedAt, options, request, skipped, null, null, diagnostics);
        }

        var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var sequenceNumber = 1;
        var connected = false;
        var loggedOn = false;
        var orderSent = false;
        var terminal = false;
        var logoutSent = false;
        string? lastMsgType = null;
        var executionReports = new List<LmaxFixExecutionReport>();
        LmaxFixDemoOrderLifecycleResult orderSubmission;
        LmaxFixOrderStatusSmokeResult? orderStatus = null;
        LmaxFixTradeCaptureSmokeResult? tradeCapture = null;

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
            var logonSeq = sequenceNumber;
            var logon = LmaxFixMarketDataCodec.BuildMessage("A", sequenceNumber++, options.FixSenderCompId!, target, [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            diagnostics.Add($"MsgSeqNum Logon={logonSeq}");
            if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT Logon {LmaxFixMarketDataCodec.SanitizeMessage(logon)}");
            using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, logon, logonTimeout.Token);
                var logonResponse = await ReadFixResponseAsync(stream, logonTimeout.Token);
                loggedOn = LmaxFixMarketDataCodec.ContainsTag(logonResponse, "35", "A");
                lastMsgType = LmaxFixMarketDataCodec.GetMsgType(logonResponse);
                if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN Logon {LmaxFixMarketDataCodec.SanitizeMessage(logonResponse)}");
            }

            if (!loggedOn)
            {
                orderSubmission = new("fix-demo-order-lifecycle", "Failed", connected, false, false, false, false, false, null, null, null, null, null, null, false, clOrdId, lastMsgType, [], startedAt, DateTimeOffset.UtcNow, "FIX trading logon was not confirmed; demo order was not sent.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
                return BuildLifecycleEvidenceResult(startedAt, options, request, orderSubmission, null, null, diagnostics);
            }

            var newOrderSeq = sequenceNumber;
            var newOrder = LmaxFixRecoveryCodec.BuildNewOrderSingle(options.FixSenderCompId!, target, sequenceNumber++, request, clOrdId, options.FixSecurityIdSource);
            diagnostics.Add($"MsgSeqNum NewOrderSingle={newOrderSeq}");
            if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT NewOrderSingle {LmaxFixMarketDataCodec.SanitizeMessage(newOrder)}");
            using (var wait = CreateTimeout(request.MaxWaitSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, newOrder, wait.Token);
                orderSent = true;
                while (!wait.IsCancellationRequested)
                {
                    string message;
                    int nextSequence;
                    try
                    {
                        (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message)) break;
                    lastMsgType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN NewOrderSingleResponse {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (lastMsgType == "8")
                    {
                        var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options);
                        executionReports.Add(normalized.Report);
                        foreach (var warning in normalized.Warnings) diagnostics.Add($"ExecutionReport warning: {warning}");
                        if (IsTerminalExecutionReport(normalized.Report))
                        {
                            terminal = true;
                            break;
                        }
                    }
                    else if (lastMsgType == "3")
                    {
                        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                        orderSubmission = new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, executionReports.Count > 0, terminal, true, "3", reject.RefTagId, reject.RefMsgType, reject.SessionRejectReason, reject.Text, "ProtocolRejected", false, clOrdId, lastMsgType, executionReports, startedAt, DateTimeOffset.UtcNow, $"NewOrderSingle was rejected at FIX session level: {reject.Text ?? "(no reject text)"}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
                        logoutSent = await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "LifecycleEvidence");
                        orderSubmission = orderSubmission with { LogoutSent = logoutSent };
                        return BuildLifecycleEvidenceResult(startedAt, options, request, orderSubmission, null, null, diagnostics);
                    }
                    else if (lastMsgType == "5")
                    {
                        diagnostics.Add($"Received MsgType=5 during order phase Text={LmaxFixMarketDataCodec.GetTag(message, "58") ?? "(none)"}");
                        break;
                    }
                }
            }

            var orderMessage = terminal
                ? "Demo order lifecycle received terminal ExecutionReport. Same-session recovery will run before logout."
                : executionReports.Count > 0
                    ? "Demo order lifecycle received ExecutionReport messages but no terminal state before recovery timeout."
                    : "Demo order lifecycle sent order but received no ExecutionReport before timeout.";
            orderSubmission = new("fix-demo-order-lifecycle", executionReports.Count > 0 ? "Ok" : "Failed", connected, loggedOn, orderSent, executionReports.Count > 0, terminal, false, null, null, null, null, null, terminal ? "TerminalExecutionReport" : null, false, clOrdId, lastMsgType, executionReports, startedAt, DateTimeOffset.UtcNow, orderMessage, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);

            if (orderSubmission.TerminalExecutionReportReceived || orderSubmission.ExecutionReports.Any(x => x.ExecType == LmaxFixExecutionReportType.Trade))
            {
                diagnostics.Add($"RecoveryPhaseSafety: AllowOrderSubmission=False AllowLiveTrading={options.AllowLiveTrading} AllowExternalConnections={options.AllowExternalConnections}");
                diagnostics.Add("Same session used for order + recovery; no second FIX logon attempted.");
                var orderStatusResult = await SendOrderStatusRequestOnSessionAsync(stream, options, target, sequenceNumber, new LmaxFixOrderStatusSmokeRequest(clOrdId, request.Account, request.LmaxInstrumentId, options.FixSecurityIdSource, request.Side == LmaxFixDemoOrderSide.Buy ? "1" : "2", null, request.MaxWaitSeconds, request.ShowFixMessages), startedAt, cancellationToken);
                sequenceNumber = orderStatusResult.NextSequenceNumber;
                orderStatus = orderStatusResult.Result;
                diagnostics.AddRange(orderStatus.Diagnostics.Select(x => $"OrderStatus: {x}"));

                var lastFill = orderSubmission.ExecutionReports.LastOrDefault(x => x.ExecType == LmaxFixExecutionReportType.Trade);
                var adjustedTradeCaptureRequest = AdjustTradeCaptureWindowAfterFill(tradeCaptureRequest, lastFill, DateTimeOffset.UtcNow, diagnostics);
                var tradeCaptureResult = await SendTradeCaptureRequestOnSessionAsync(stream, options, target, sequenceNumber, adjustedTradeCaptureRequest, startedAt, cancellationToken);
                sequenceNumber = tradeCaptureResult.NextSequenceNumber;
                tradeCapture = tradeCaptureResult.Result;
                diagnostics.AddRange(tradeCapture.Diagnostics.Select(x => $"TradeCapture: {x}"));
            }
            else
            {
                diagnostics.Add("Terminal/fill ExecutionReport was not available; same-session read-only recovery was not run.");
            }

            logoutSent = await TrySendLogoutAsync(stream, options, target, sequenceNumber, diagnostics, "LifecycleEvidence");
            orderSubmission = orderSubmission with { LogoutSent = logoutSent, CompletedAtUtc = DateTimeOffset.UtcNow };
            return BuildLifecycleEvidenceResult(startedAt, options, request, orderSubmission, orderStatus, tradeCapture, diagnostics);
        }
        catch (OperationCanceledException)
        {
            orderSubmission = new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, executionReports.Count > 0, terminal, false, null, null, null, null, null, null, logoutSent, clOrdId, lastMsgType, executionReports, startedAt, DateTimeOffset.UtcNow, "FIX lifecycle evidence command timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
            return BuildLifecycleEvidenceResult(startedAt, options, request, orderSubmission, orderStatus, tradeCapture, diagnostics);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or ArgumentException)
        {
            orderSubmission = new("fix-demo-order-lifecycle", "Failed", connected, loggedOn, orderSent, executionReports.Count > 0, terminal, false, null, null, null, null, null, null, logoutSent, clOrdId, lastMsgType, executionReports, startedAt, DateTimeOffset.UtcNow, $"FIX lifecycle evidence command failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), decisions, diagnostics);
            return BuildLifecycleEvidenceResult(startedAt, options, request, orderSubmission, orderStatus, tradeCapture, diagnostics);
        }
    }

    private static LmaxFixLifecycleEvidenceResult BuildLifecycleEvidenceResult(
        DateTimeOffset startedAt,
        LmaxConnectivityLabOptions options,
        LmaxFixDemoOrderRequest request,
        LmaxFixDemoOrderLifecycleResult orderSubmission,
        LmaxFixOrderStatusSmokeResult? orderStatus,
        LmaxFixTradeCaptureSmokeResult? tradeCapture,
        IReadOnlyList<string> diagnostics)
    {
        var evidence = LmaxFixLifecycleEvidenceBuilder.Build(request, orderSubmission, orderStatus, tradeCapture);
        var failedChecks = evidence.ConsistencyChecks.Count(x => x.Status == LmaxFixLifecycleConsistencyStatus.Failed);
        var orderFilled = evidence.FillExecutionReportCount > 0 && string.Equals(evidence.FinalOrdStatus, LmaxFixOrderStatus.Filled.ToString(), StringComparison.Ordinal);
        var status = orderSubmission.Status == "Failed"
            ? "Failed"
            : orderFilled && failedChecks > 0
                ? "PartiallySucceeded"
                : failedChecks > 0
                    ? "Failed"
                    : orderSubmission.Status == "Skipped"
                        ? "Skipped"
                        : "Ok";
        var message = status switch
        {
            "Ok" => "FIX lifecycle evidence report completed. No data was persisted.",
            "PartiallySucceeded" => $"Demo order filled, but recovery evidence is incomplete: OrderStatusReceived={evidence.OrderStatusReceived}; TradeCaptureReceived={evidence.TradeCaptureReceived}; FailedConsistencyChecks={failedChecks}. No data was persisted.",
            "Skipped" => "FIX lifecycle evidence report was skipped before live submission. No order was submitted.",
            _ => failedChecks > 0
                ? $"FIX lifecycle evidence report found {failedChecks} failed consistency check(s). No data was persisted."
                : orderSubmission.Message
        };

        return new("fix-demo-lifecycle-evidence", status, orderSubmission, orderStatus, tradeCapture, evidence, startedAt, DateTimeOffset.UtcNow, message, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
    }

    private sealed record SameSessionOrderStatusResult(LmaxFixOrderStatusSmokeResult Result, int NextSequenceNumber);

    private async Task<SameSessionOrderStatusResult> SendOrderStatusRequestOnSessionAsync(Stream stream, LmaxConnectivityLabOptions options, string target, int sequenceNumber, LmaxFixOrderStatusSmokeRequest request, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string> { "SameSession=True", "No second FIX logon attempted." };
        var reports = new List<LmaxFixExecutionReport>();
        var requestSent = false;
        var requestRejected = false;
        string? rejectRefTagId = null;
        string? rejectRefMsgType = null;
        string? rejectText = null;
        var requestSeq = sequenceNumber;
        try
        {
            var orderStatusRequest = LmaxFixRecoveryCodec.BuildOrderStatusRequest(options.FixSenderCompId!, target, sequenceNumber++, request.ClOrdId!, request.Account, request.SecurityId, request.SecurityIdSource, request.Side, request.OrdStatusReqId);
            diagnostics.Add($"MsgSeqNum OrderStatusRequest={requestSeq}");
            if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(orderStatusRequest)}");
            using var wait = CreateTimeout(request.MaxWaitSeconds, cancellationToken);
            await WriteAsciiAsync(stream, orderStatusRequest, wait.Token);
            requestSent = true;
            while (!wait.IsCancellationRequested)
            {
                string message;
                int nextSequence;
                try
                {
                    (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                sequenceNumber = nextSequence;
                if (string.IsNullOrWhiteSpace(message)) break;
                var msgType = LmaxFixMarketDataCodec.GetMsgType(message);
                if (request.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                if (msgType == "8")
                {
                    var normalized = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options);
                    reports.Add(normalized.Report);
                    foreach (var warning in normalized.Warnings) diagnostics.Add($"ExecutionReport warning: {warning}");
                    break;
                }

                if (msgType == "3")
                {
                    var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                    requestRejected = true;
                    rejectRefTagId = reject.RefTagId;
                    rejectRefMsgType = reject.RefMsgType;
                    rejectText = reject.Text;
                    break;
                }

                if (msgType == "5")
                {
                    rejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        var latest = reports.LastOrDefault();
        var result = reports.Count > 0
            ? new LmaxFixOrderStatusSmokeResult("fix-order-status-smoke", "Ok", true, true, requestSent, true, false, null, null, null, request.ClOrdId, latest?.OrderId, latest?.OrdStatus.ToString(), reports, startedAt, DateTimeOffset.UtcNow, false, $"Received same-session ExecutionReport for ClOrdID={request.ClOrdId}; OrdStatus={latest?.OrdStatus}. No data was persisted.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics)
            : requestRejected
                ? new LmaxFixOrderStatusSmokeResult("fix-order-status-smoke", "Failed", true, true, requestSent, false, true, rejectRefTagId, rejectRefMsgType, rejectText, request.ClOrdId, null, null, [], startedAt, DateTimeOffset.UtcNow, false, $"OrderStatusRequest was rejected at FIX session level: {rejectText ?? "(no reject text)"}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics)
                : new LmaxFixOrderStatusSmokeResult("fix-order-status-smoke", "Failed", true, true, requestSent, false, false, null, null, rejectText, request.ClOrdId, null, null, [], startedAt, DateTimeOffset.UtcNow, false, "OrderStatusRequest timed out before ExecutionReport or session reject was received on the same session.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        return new(result, sequenceNumber);
    }

    private sealed record SameSessionTradeCaptureResult(LmaxFixTradeCaptureSmokeResult Result, int NextSequenceNumber);

    private async Task<SameSessionTradeCaptureResult> SendTradeCaptureRequestOnSessionAsync(Stream stream, LmaxConnectivityLabOptions options, string target, int sequenceNumber, LmaxFixTradeCaptureRequestOptions requestOptions, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string> { "SameSession=True", "No second FIX logon attempted." };
        var reports = new List<LmaxFixTradeCaptureReport>();
        var requestSent = false;
        var ackReceived = false;
        var ackAccepted = false;
        var requestRejected = false;
        string? ackRejectText = null;
        string? rejectMsgType = null;
        string? rejectRefTagId = null;
        string? rejectRefMsgType = null;
        string? rejectReasonCode = null;
        string? rejectText = null;
        string? lastReceivedMsgType = null;
        int? expectedTradeReportCount = null;
        var noMoreReports = false;
        var timedOutWaitingForReports = false;
        var requestSeq = sequenceNumber;
        var tradeRequestId = LmaxFixRecoveryCodec.GenerateTradeRequestId(DateTimeOffset.UtcNow, sequenceNumber);
        var request = LmaxFixRecoveryCodec.BuildTradeCaptureReportRequest(options.FixSenderCompId!, target, sequenceNumber++, tradeRequestId, requestOptions);
        diagnostics.Add($"MsgSeqNum TradeCaptureReportRequest={requestSeq}");
        diagnostics.Add($"TradeRequestID={tradeRequestId}");
        if (requestOptions.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(request)}");
        using (var wait = CreateTimeout(requestOptions.MaxWaitSeconds, cancellationToken))
        {
            await WriteAsciiAsync(stream, request, wait.Token);
            requestSent = true;
            while (!wait.IsCancellationRequested && reports.Count < requestOptions.MaxReports)
            {
                string message;
                int nextSequence;
                try
                {
                    (message, nextSequence) = await ReadMarketDataResponseAsync(stream, options, target, sequenceNumber, wait.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    timedOutWaitingForReports = true;
                    break;
                }

                sequenceNumber = nextSequence;
                if (string.IsNullOrWhiteSpace(message)) break;
                lastReceivedMsgType = LmaxFixMarketDataCodec.GetMsgType(message);
                if (requestOptions.ShowFixMessages || options.ShowFixMessages) diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                if (lastReceivedMsgType == "AQ")
                {
                    var ack = LmaxFixRecoveryCodec.ParseTradeCaptureAck(message);
                    ackReceived = true;
                    ackAccepted = ack.Accepted;
                    ackRejectText = ack.Text;
                    expectedTradeReportCount = ack.TotNumTradeReports;
                    diagnostics.Add($"TradeCaptureAck: TradeRequestID={ack.RequestId ?? "(missing)"} TotNumTradeReports={expectedTradeReportCount?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"} Result={ack.Result ?? "(missing)"} Status={ack.Status ?? "(missing)"}");
                    if (ackAccepted && expectedTradeReportCount == 0)
                    {
                        noMoreReports = true;
                        break;
                    }

                    if (!ackAccepted) break;
                }
                else if (lastReceivedMsgType == "AE")
                {
                    var normalized = LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(message, options);
                    reports.Add(normalized.Report);
                    foreach (var warning in normalized.Warnings) diagnostics.Add($"TradeCaptureReport warning: {warning}");
                    if (normalized.Report.LastReportRequested || expectedTradeReportCount is not null && reports.Count >= expectedTradeReportCount.Value)
                    {
                        noMoreReports = true;
                        break;
                    }
                }
                else if (lastReceivedMsgType == "3")
                {
                    var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                    rejectMsgType = "3";
                    rejectRefTagId = reject.RefTagId;
                    rejectRefMsgType = reject.RefMsgType;
                    rejectReasonCode = reject.SessionRejectReason;
                    rejectText = reject.Text;
                    ackRejectText = reject.Text;
                    requestRejected = string.Equals(reject.RefMsgType, "AD", StringComparison.Ordinal);
                    break;
                }
                else if (lastReceivedMsgType == "5")
                {
                    ackRejectText = LmaxFixMarketDataCodec.GetTag(message, "58");
                    break;
                }
            }
        }

        var status = ResolveTradeCaptureStatus(ackReceived, ackAccepted, requestRejected, expectedTradeReportCount, reports.Count, timedOutWaitingForReports);
        var messageText = ResolveTradeCaptureMessage(ackReceived, ackAccepted, requestRejected, rejectText, expectedTradeReportCount, reports.Count, timedOutWaitingForReports);
        var result = new LmaxFixTradeCaptureSmokeResult("fix-trade-capture-smoke", status, true, true, requestSent, ackReceived, ackAccepted, requestRejected, ackRejectText, rejectMsgType, rejectRefTagId, rejectRefMsgType, rejectReasonCode, rejectText, lastReceivedMsgType, expectedTradeReportCount, noMoreReports, false, reports.Count, reports.Any(x => x.LastReportRequested) || noMoreReports, reports, startedAt, DateTimeOffset.UtcNow, messageText, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), diagnostics);
        return new(result, sequenceNumber);
    }

    public static LmaxFixTradeCaptureRequestOptions AdjustTradeCaptureWindowAfterFill(LmaxFixTradeCaptureRequestOptions requested, LmaxFixExecutionReport? lastFill, DateTimeOffset now, ICollection<string> diagnostics)
    {
        var fillTime = lastFill?.TransactTimeUtc ?? now;
        var requestedLookback = requested.EndUtc > requested.StartUtc ? requested.EndUtc - requested.StartUtc : TimeSpan.FromMinutes(1440);
        var lookback = requestedLookback < TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : requestedLookback;
        var startUtc = fillTime.Subtract(lookback).ToUniversalTime();
        var endUtc = new[] { now.ToUniversalTime(), fillTime.AddMinutes(1).ToUniversalTime() }.Max();
        if (endUtc <= fillTime) endUtc = fillTime.AddMinutes(1).ToUniversalTime();

        diagnostics.Add($"FillTransactTimeUtc={fillTime:O}");
        diagnostics.Add($"TradeCaptureStartUtc={startUtc:O}");
        diagnostics.Add($"TradeCaptureEndUtc={endUtc:O}");
        return requested with { StartUtc = startUtc, EndUtc = endUtc };
    }

    private static LmaxConnectivityLabOptions CopyOptions(LmaxConnectivityLabOptions source)
        => new()
        {
            Enabled = source.Enabled,
            EnvironmentName = source.EnvironmentName,
            AllowExternalConnections = source.AllowExternalConnections,
            AllowOrderSubmission = source.AllowOrderSubmission,
            AllowLiveTrading = source.AllowLiveTrading,
            DryRun = source.DryRun,
            VenueName = source.VenueName,
            AccountCode = source.AccountCode,
            AccountApiBaseUrl = source.AccountApiBaseUrl,
            PublicDataApiBaseUrl = source.PublicDataApiBaseUrl,
            AccountApiAuthMode = source.AccountApiAuthMode,
            AccountApiUsername = source.AccountApiUsername,
            AccountApiPassword = source.AccountApiPassword,
            AccountApiKey = source.AccountApiKey,
            AccountApiKeyHeaderName = source.AccountApiKeyHeaderName,
            AccountApiBearerToken = source.AccountApiBearerToken,
            AccountApiRequestTimeoutSeconds = source.AccountApiRequestTimeoutSeconds,
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

    private static string ResolveTradeCaptureStatus(bool ackReceived, bool ackAccepted, bool requestRejected, int? expectedTradeReportCount, int reportCount, bool timedOut)
    {
        if (requestRejected || !ackReceived || !ackAccepted) return "Failed";
        if (expectedTradeReportCount == 0) return "Ok";
        if (expectedTradeReportCount is > 0 && reportCount >= expectedTradeReportCount.Value) return "Ok";
        if (expectedTradeReportCount is > 0 && timedOut) return "PartiallySucceeded";
        return timedOut ? "Failed" : "Ok";
    }

    private static string ResolveTradeCaptureMessage(bool ackReceived, bool ackAccepted, bool requestRejected, string? rejectText, int? expectedTradeReportCount, int reportCount, bool timedOut)
    {
        if (requestRejected) return $"TradeCaptureReportRequest was rejected by FIX session reject: {rejectText ?? "(no reject text)"}";
        if (!ackReceived) return "No TradeCaptureReportRequestAck was received before timeout.";
        if (!ackAccepted) return "Trade capture request was rejected or not accepted.";
        if (expectedTradeReportCount == 0) return "Trade capture request accepted; no trade reports returned for the requested window.";
        if (expectedTradeReportCount is > 0 && reportCount >= expectedTradeReportCount.Value) return $"Trade capture request accepted; received {reportCount} of {expectedTradeReportCount.Value} expected trade reports. No data was persisted.";
        if (expectedTradeReportCount is > 0 && timedOut) return $"Trade capture request accepted, but timed out after receiving {reportCount} of {expectedTradeReportCount.Value} expected trade reports.";
        if (expectedTradeReportCount is null && timedOut && reportCount == 0) return "AQ accepted but no TotNumTradeReports was provided and no AE reports arrived before timeout.";
        if (expectedTradeReportCount is null && timedOut) return $"AQ accepted but no TotNumTradeReports was provided; received {reportCount} AE reports before timeout.";
        return "Trade capture request accepted. No data was persisted.";
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
