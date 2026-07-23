namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Globalization;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;

public sealed partial class RawLmaxFixSessionClient
{
    public async Task<LmaxFixArch7bKnownOrderResult> Arch7bKnownOrderLifecycleAsync(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        if (request.Activation == LmaxFixArch7bActivation.Disabled)
            return LmaxFixArch7bKnownOrderResult.Skipped("ARCH7B_EXECUTION_DISABLED_BY_DEFAULT");

        if (request.Activation == LmaxFixArch7bActivation.DryRun)
        {
            var plan = LmaxFixArch7bKnownOrderContract.BuildDryRunPlan(options, request);
            return new(
                "fix-arch7b-known-order-lifecycle",
                "Ok",
                false,
                false,
                false,
                false,
                false,
                0,
                false,
                null,
                [],
                [
                    plan.OpeningNewOrderSingleSanitized,
                    plan.OpeningCancelRequestSanitized,
                    plan.FlattenNewOrderSingleSanitized,
                    plan.OpeningOrderStatusRequestSanitized,
                    plan.FlattenOrderStatusRequestSanitized,
                    "ARCH7B_DRY_RUN_NO_NETWORK_NO_SEND"
                ],
                startedAt,
                DateTimeOffset.UtcNow);
        }

        var diagnostics = new List<string>();
        var blockers = LmaxFixArch7bKnownOrderContract.Validate(options, request, startedAt).ToList();
        var qualificationSession = arch7bObserver as ILmaxFixArch7bQualificationSession;
        if (arch7bObserver is null || !arch7bObserver.IsDurable)
            blockers.Add("ARCH7B_DURABLE_PERSISTENCE_OBSERVER_REQUIRED");
        if (qualificationSession is null)
            blockers.Add("ARCH7B_DURABLE_QUALIFICATION_SESSION_REQUIRED");
        if (blockers.Count != 0)
            return LmaxFixArch7bKnownOrderResult.Skipped(blockers[0], blockers);

        var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        var reports = new List<LmaxFixExecutionReport>();
        var knownClientOrderIds = new HashSet<string>(StringComparer.Ordinal)
        {
            request.OpeningClientOrderId,
            request.CancelClientOrderId,
            request.FlattenClientOrderId
        };
        var orderIdsByClientOrderId = new Dictionary<string, string>(StringComparer.Ordinal);
        var sequenceNumber = 1;
        var openingSent = false;
        var cancelSent = false;
        var flattenSent = false;
        var statusRequestCount = 0;
        LmaxFixArch7bRecoveryState? recovery = null;
        LmaxFixArch7bRecoveryPlan? recoveryPlan = null;
        var connected = false;
        var loggedOn = false;
        var logoutSent = false;
        string? blocker = null;
        long? lastInboundSequenceNumber = null;

        try
        {
            await qualificationSession!.InitializeAsync(request, cancellationToken);
            recovery = await qualificationSession.LoadRecoveryStateAsync(
                request,
                cancellationToken);
            statusRequestCount = recovery.OrderStatusRequestCount;
            recoveryPlan = LmaxFixArch7bRecoveryPlanner.Build(recovery);
            await arch7bObserver!.RecordSessionEventAsync(
                request,
                "KILL_SWITCH_ARMED_BEFORE_FIX_LOGON",
                null,
                startedAt,
                cancellationToken);

            using var tcp = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                await tcp.ConnectAsync(options.FixOrderHost!, options.FixOrderPort!.Value, connectTimeout.Token);
                connected = true;
            }

            Stream rawStream;
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
                rawStream = options.UseTls
                    ? await CreateTlsStreamAsync(tcp, options.FixOrderHost!, connectTimeout.Token)
                    : tcp.GetStream();

            await using var stream = rawStream;
            var logonSequence = sequenceNumber++;
            var logon = LmaxFixMarketDataCodec.BuildMessage("A", logonSequence, options.FixSenderCompId!, target,
            [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, logon, logonTimeout.Token);
                var response = await ReadFixResponseAsync(stream, logonTimeout.Token);
                loggedOn = LmaxFixMarketDataCodec.ContainsTag(response, "35", "A");
                if (!long.TryParse(
                        LmaxFixMarketDataCodec.GetTag(response, "34"),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var logonInboundSequence) || logonInboundSequence <= 0)
                    throw new InvalidOperationException("ARCH7B_FIX_SEQUENCE_INVALID");
                lastInboundSequenceNumber = logonInboundSequence;
            }

            if (!loggedOn)
                return Result("Failed", "ARCH7B_FIX_LOGON_NOT_CONFIRMED");

            await arch7bObserver.RecordSessionEventAsync(
                request,
                "FIX_LOGON_CONFIRMED",
                logonSequence,
                DateTimeOffset.UtcNow,
                cancellationToken);

            if (!recoveryPlan.MaySendOpeningNewOrderSingle)
            {
                openingSent = true;
                diagnostics.Add("ARCH7B_RECOVERY_OPEN_SEND_INTENT_REUSED_NO_RESEND");
                if (recoveryPlan.QueryOpeningKnownOrder)
                {
                    await SendKnownStatusRequestAsync(
                        stream,
                        request.OpeningClientOrderId,
                        "BUY",
                        "1",
                        cancellationToken);
                }
            }
            else
            {
                var openingRequest = LmaxFixArch7bKnownOrderContract.DemoLimitRequest(
                    request.AccountId,
                    LmaxFixDemoOrderSide.Buy,
                    Arch7bKnownOrderQualificationPolicy.VenueQuantity,
                    request.OpeningLimitPrice,
                    request.OpeningClientOrderId);
                var opening = LmaxFixRecoveryCodec.BuildNewOrderSingle(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    openingRequest,
                    request.OpeningClientOrderId,
                    Arch7bKnownOrderQualificationPolicy.SecurityIdSource);
                await PersistThenSendAsync(
                    stream,
                    "OPEN",
                    "D",
                    request.OpeningClientOrderId,
                    null,
                    "BUY",
                    Arch7bKnownOrderQualificationPolicy.VenueQuantity,
                    request.OpeningLimitPrice,
                    opening,
                    sequenceNumber++,
                    cancellationToken);
                openingSent = true;
            }

            await ReadUntilAsync(
                stream,
                request.OpeningCancelAtUtc,
                () => OpeningTerminal() || OpeningFilledQuantity() >= Arch7bKnownOrderQualificationPolicy.VenueQuantity,
                cancellationToken);
            if (blocker is not null)
                return Result("Failed", blocker);

            var openingFilled = OpeningFilledQuantity();
            if (!OpeningTerminal() || OpeningLeavesQuantity() > 0m)
            {
                if (!recoveryPlan.MaySendOpeningResidualCancel)
                {
                    cancelSent = true;
                    diagnostics.Add("ARCH7B_RECOVERY_CANCEL_SEND_INTENT_REUSED_NO_RESEND");
                    await SendKnownStatusRequestAsync(
                        stream,
                        request.OpeningClientOrderId,
                        "BUY",
                        "1",
                        cancellationToken);
                }
                else
                {
                    var cancel = LmaxFixRecoveryCodec.BuildOrderCancelRequest(
                        options.FixSenderCompId!,
                        target,
                        sequenceNumber,
                        request.CancelClientOrderId,
                        request.OpeningClientOrderId,
                        Arch7bKnownOrderQualificationPolicy.Symbol,
                        "1",
                        Arch7bKnownOrderQualificationPolicy.VenueQuantity,
                        Arch7bKnownOrderQualificationPolicy.SecurityId,
                        Arch7bKnownOrderQualificationPolicy.SecurityIdSource);
                    await PersistThenSendAsync(
                        stream,
                        "OPEN_RESIDUAL_CANCEL",
                        "F",
                        request.CancelClientOrderId,
                        request.OpeningClientOrderId,
                        "BUY",
                        Arch7bKnownOrderQualificationPolicy.VenueQuantity,
                        null,
                        cancel,
                        sequenceNumber++,
                        cancellationToken);
                    cancelSent = true;
                }
                await ReadUntilAsync(stream, request.DeadlineUtc, OpeningTerminal, cancellationToken);
                openingFilled = OpeningFilledQuantity();
            }

            if (!OpeningTerminal())
            {
                await SendKnownStatusRequestAsync(
                    stream,
                    request.OpeningClientOrderId,
                    "BUY",
                    "1",
                    cancellationToken);
                await ReadUntilAsync(stream, request.DeadlineUtc, OpeningTerminal, cancellationToken);
            }

            if (blocker is not null)
                return Result("Failed", blocker);
            if (!OpeningTerminal() || OpeningLeavesQuantity() > 0m)
                return Result("Failed", "ARCH7B_OPENING_TERMINAL_NOT_CONFIRMED");
            if (openingFilled == 0m)
            {
                logoutSent = await SafeLogoutAsync(stream, "ARCH7B_OPENING_ORDER_NOT_FILLED");
                return Result("Failed", "ARCH7B_OPENING_ORDER_NOT_FILLED");
            }
            if (openingFilled > Arch7bKnownOrderQualificationPolicy.VenueQuantity ||
                openingFilled % Arch7bKnownOrderQualificationPolicy.QuantityIncrement != 0m)
                return Result("Failed", "ARCH7B_OPENING_FILL_QUANTITY_OUT_OF_BOUNDS");

            if (!recoveryPlan.MaySendFlattenNewOrderSingle)
            {
                flattenSent = true;
                diagnostics.Add("ARCH7B_RECOVERY_FLATTEN_SEND_INTENT_REUSED_NO_RESEND");
                if (recoveryPlan.QueryFlattenKnownOrder)
                {
                    await SendKnownStatusRequestAsync(
                        stream,
                        request.FlattenClientOrderId,
                        "SELL",
                        "2",
                        cancellationToken);
                }
            }
            else
            {
                var flattenRequest = LmaxFixArch7bKnownOrderContract.DemoLimitRequest(
                    request.AccountId,
                    LmaxFixDemoOrderSide.Sell,
                    openingFilled,
                    request.FlattenLimitPrice,
                    request.FlattenClientOrderId);
                var flatten = LmaxFixRecoveryCodec.BuildNewOrderSingle(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    flattenRequest,
                    request.FlattenClientOrderId,
                    Arch7bKnownOrderQualificationPolicy.SecurityIdSource);
                await PersistThenSendAsync(
                    stream,
                    "FLATTEN",
                    "D",
                    request.FlattenClientOrderId,
                    null,
                    "SELL",
                    openingFilled,
                    request.FlattenLimitPrice,
                    flatten,
                    sequenceNumber++,
                    cancellationToken);
                flattenSent = true;
            }
            await ReadUntilAsync(
                stream,
                request.DeadlineUtc,
                () => FlattenTerminal() && FlattenFilledQuantity() == openingFilled,
                cancellationToken);

            if (blocker is not null)
                return Result("Failed", blocker);
            if (!FlattenTerminal() || FlattenLeavesQuantity() > 0m || FlattenFilledQuantity() != openingFilled)
            {
                if (statusRequestCount < Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount)
                {
                    await SendKnownStatusRequestAsync(
                        stream,
                        request.FlattenClientOrderId,
                        "SELL",
                        "2",
                        cancellationToken);
                    await ReadUntilAsync(
                        stream,
                        request.DeadlineUtc,
                        () => FlattenTerminal() && FlattenFilledQuantity() == openingFilled,
                        cancellationToken);
                }
            }

            if (!FlattenTerminal() || FlattenLeavesQuantity() > 0m || FlattenFilledQuantity() != openingFilled)
                return Result("Failed", "ARCH7B_FLATTEN_NOT_CONFIRMED");

            await arch7bObserver.RecordSessionEventAsync(
                request,
                "FIX_SESSION_SEQUENCE_CONTINUITY_VALIDATED",
                lastInboundSequenceNumber,
                DateTimeOffset.UtcNow,
                cancellationToken);
            var lifecycle = await qualificationSession.FinalizeReconciliationAsync(
                request,
                cancellationToken);
            if (!lifecycle.Qualified || !lifecycle.Flat)
                return Result("Failed", "ARCH7B_FINAL_RECONCILIATION_NOT_FLAT");

            logoutSent = await SafeLogoutAsync(stream, "ARCH7B_KNOWN_ORDER_LIFECYCLE_FLAT");
            return Result("Ok", null);

            async Task PersistThenSendAsync(
                Stream activeStream,
                string role,
                string messageType,
                string clientOrderId,
                string? originalClientOrderId,
                string side,
                decimal quantity,
                decimal? limitPrice,
                string payload,
                int fixSequenceNumber,
                CancellationToken token)
            {
                var intent = new LmaxFixArch7bOutboundIntent(
                    request.QualificationRunId,
                    role,
                    messageType,
                    clientOrderId,
                    originalClientOrderId,
                    side,
                    quantity,
                    limitPrice,
                    request.BboSnapshotSha256,
                    Sha256(payload),
                    DateTimeOffset.UtcNow);
                await arch7bObserver.RecordSendIntentAsync(request, intent, token);
                if (request.ShowFixMessages || options.ShowFixMessages)
                    diagnostics.Add($"OUT {LmaxFixMarketDataCodec.SanitizeMessage(payload)}");
                await WriteAsciiAsync(activeStream, payload, token);
                await arch7bObserver.RecordSessionEventAsync(
                    request,
                    $"FIX_APPLICATION_MESSAGE_SENT_{messageType}_{role}",
                    fixSequenceNumber,
                    DateTimeOffset.UtcNow,
                    token);
            }

            async Task SendKnownStatusRequestAsync(
                Stream activeStream,
                string clientOrderId,
                string side,
                string fixSide,
                CancellationToken token)
            {
                if (++statusRequestCount > Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount)
                    throw new InvalidOperationException("ARCH7B_ORDER_STATUS_REQUEST_BUDGET_EXCEEDED");
                var status = LmaxFixRecoveryCodec.BuildOrderStatusRequest(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    clientOrderId,
                    request.AccountId,
                    Arch7bKnownOrderQualificationPolicy.SecurityId,
                    Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
                    fixSide);
                await PersistThenSendAsync(
                    activeStream,
                    clientOrderId == request.OpeningClientOrderId ? "OPEN_STATUS" : "FLATTEN_STATUS",
                    "H",
                    clientOrderId,
                    null,
                    side,
                    0m,
                    null,
                    status,
                    sequenceNumber++,
                    token);
            }

            async Task ReadUntilAsync(
                Stream activeStream,
                DateTimeOffset deadlineUtc,
                Func<bool> completed,
                CancellationToken token)
            {
                while (!completed() && blocker is null && DateTimeOffset.UtcNow < deadlineUtc)
                {
                    var remaining = deadlineUtc - DateTimeOffset.UtcNow;
                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
                    wait.CancelAfter(remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining);
                    string message;
                    int nextSequence;
                    try
                    {
                        (message, nextSequence) = await ReadArch7bResponseAsync(
                            activeStream,
                            sequenceNumber,
                            wait.Token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        return;
                    }

                    sequenceNumber = nextSequence;
                    if (string.IsNullOrWhiteSpace(message))
                        continue;
                    var messageType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (request.ShowFixMessages || options.ShowFixMessages)
                        diagnostics.Add($"IN {LmaxFixMarketDataCodec.SanitizeMessage(message)}");
                    if (messageType == "8")
                    {
                        var report = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, options).Report;
                        reports.Add(report);
                        await arch7bObserver.RecordExecutionReportAsync(request, report, token);
                        blocker = ValidateKnownReport(report);
                    }
                    else if (messageType == "3")
                    {
                        var reject = LmaxFixRecoveryCodec.ParseSessionReject(message);
                        blocker = $"ARCH7B_FIX_SESSION_REJECT:{reject.RefMsgType ?? "UNKNOWN"}:{reject.RefTagId ?? "UNKNOWN"}";
                    }
                    else if (messageType == "5")
                    {
                        blocker = "ARCH7B_UNEXPECTED_BROKER_LOGOUT";
                    }
                }
            }

            async Task<(string Message, int NextSequenceNumber)> ReadArch7bResponseAsync(
                Stream activeStream,
                int nextOutboundSequenceNumber,
                CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    var message = await ReadAnyFixMessageAsync(activeStream, token);
                    if (!long.TryParse(
                            LmaxFixMarketDataCodec.GetTag(message, "34"),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var inboundSequenceNumber) || inboundSequenceNumber <= 0)
                        throw new InvalidOperationException("ARCH7B_FIX_SEQUENCE_INVALID");
                    var possDup = LmaxFixMarketDataCodec.GetTag(message, "43") == "Y";
                    if (lastInboundSequenceNumber is { } previous)
                    {
                        if (inboundSequenceNumber > previous + 1)
                            throw new InvalidOperationException(
                                $"ARCH7B_FIX_SEQUENCE_GAP_UNRESOLVED:{previous + 1}-{inboundSequenceNumber - 1}");
                        if (inboundSequenceNumber <= previous && !possDup)
                            throw new InvalidOperationException(
                                $"ARCH7B_FIX_SEQUENCE_REWIND_WITHOUT_POSSDUP:{inboundSequenceNumber}");
                    }
                    if (lastInboundSequenceNumber is null ||
                        inboundSequenceNumber > lastInboundSequenceNumber)
                        lastInboundSequenceNumber = inboundSequenceNumber;

                    var messageType = LmaxFixMarketDataCodec.GetMsgType(message);
                    if (messageType == "1")
                    {
                        var testRequestId = LmaxFixMarketDataCodec.GetTag(message, "112");
                        IReadOnlyList<(string Tag, string Value)> heartbeatFields =
                            string.IsNullOrWhiteSpace(testRequestId)
                                ? []
                                : [("112", testRequestId)];
                        var heartbeat = LmaxFixMarketDataCodec.BuildMessage(
                            "0",
                            nextOutboundSequenceNumber++,
                            options.FixSenderCompId!,
                            target,
                            heartbeatFields);
                        await WriteAsciiAsync(activeStream, heartbeat, token);
                        continue;
                    }
                    if (messageType == "0")
                        continue;
                    return (message, nextOutboundSequenceNumber);
                }
                return (string.Empty, nextOutboundSequenceNumber);
            }

            string? ValidateKnownReport(LmaxFixExecutionReport report)
            {
                if (string.IsNullOrWhiteSpace(report.ClOrdId) ||
                    (!knownClientOrderIds.Contains(report.ClOrdId) &&
                     (string.IsNullOrWhiteSpace(report.OrigClOrdId) ||
                      !knownClientOrderIds.Contains(report.OrigClOrdId))))
                    return "ARCH7B_UNKNOWN_CLORDID";
                if (report.Account != Arch7bKnownOrderQualificationPolicy.DemoAccountId)
                    return "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH";
                if (report.Account == Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId)
                    return "ARCH7B_REAL_ACCOUNT_FORBIDDEN";
                if (report.Symbol != Arch7bKnownOrderQualificationPolicy.Symbol ||
                    report.SecurityId != Arch7bKnownOrderQualificationPolicy.SecurityId)
                    return "ARCH7B_EXECUTION_REPORT_INSTRUMENT_MISMATCH";
                if (report.FixSequenceNumber is null || report.FixSequenceNumber <= 0)
                    return "ARCH7B_FIX_SEQUENCE_INVALID";
                if (string.IsNullOrWhiteSpace(report.OrderId))
                    return "ARCH7B_UNKNOWN_ORDERID";

                var identityClientOrderId = !string.IsNullOrWhiteSpace(report.OrigClOrdId)
                    ? report.OrigClOrdId
                    : report.ClOrdId;
                if (orderIdsByClientOrderId.TryGetValue(identityClientOrderId!, out var knownOrderId) &&
                    !knownOrderId.Equals(report.OrderId, StringComparison.Ordinal))
                    return "ARCH7B_UNKNOWN_ORDERID";
                orderIdsByClientOrderId[identityClientOrderId!] = report.OrderId;
                return null;
            }

            decimal OpeningFilledQuantity() =>
                LatestQuantity(request.OpeningClientOrderId, value => value.CumQty,
                    recovery!.OpeningCumulativeQuantity);
            decimal OpeningLeavesQuantity() =>
                LatestQuantity(request.OpeningClientOrderId, value => value.LeavesQty,
                    recovery!.OpeningLeavesQuantity);
            decimal FlattenFilledQuantity() =>
                LatestQuantity(request.FlattenClientOrderId, value => value.CumQty,
                    recovery!.FlattenCumulativeQuantity);
            decimal FlattenLeavesQuantity() =>
                LatestQuantity(request.FlattenClientOrderId, value => value.LeavesQty,
                    recovery!.FlattenLeavesQuantity);
            bool OpeningTerminal() => recovery!.OpeningTerminal ||
                LatestRelated(request.OpeningClientOrderId) is { } report && IsTerminalExecutionReport(report);
            bool FlattenTerminal() => recovery!.FlattenTerminal ||
                LatestRelated(request.FlattenClientOrderId) is { } report && IsTerminalExecutionReport(report);

            decimal LatestQuantity(string clientOrderId, Func<LmaxFixExecutionReport, decimal?> selector, decimal baseline)
                => LatestRelated(clientOrderId) is { } report ? selector(report) ?? baseline : baseline;

            LmaxFixExecutionReport? LatestRelated(string clientOrderId)
                => reports
                    .Where(value =>
                        value.ClOrdId == clientOrderId ||
                        value.OrigClOrdId == clientOrderId)
                    .OrderBy(value => value.TransactTimeUtc)
                    .ThenBy(value => value.FixSequenceNumber)
                    .LastOrDefault();

            async Task<bool> SafeLogoutAsync(Stream activeStream, string reason)
            {
                var sent = await TrySendLogoutAsync(
                    activeStream,
                    options,
                    target,
                    sequenceNumber,
                    diagnostics,
                    reason);
                if (sent)
                    await arch7bObserver.RecordSessionEventAsync(
                        request,
                        "FIX_LOGOUT_SENT",
                        sequenceNumber,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);
                return sent;
            }

            LmaxFixArch7bKnownOrderResult Result(string status, string? resultBlocker)
                => new(
                    "fix-arch7b-known-order-lifecycle",
                    status,
                    connected,
                    loggedOn,
                    openingSent,
                    cancelSent,
                    flattenSent,
                    statusRequestCount,
                    logoutSent,
                    resultBlocker,
                    reports,
                    diagnostics,
                    startedAt,
                    DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultOutside("Failed", "ARCH7B_LIFECYCLE_DEADLINE_EXCEEDED");
        }
        catch (Exception exception) when (
            exception is SocketException or IOException or AuthenticationException or
            ArgumentException or InvalidOperationException)
        {
            diagnostics.Add($"{exception.GetType().Name}:{exception.Message}");
            return ResultOutside("Failed", $"ARCH7B_RUNNER_FAILURE:{exception.Message}");
        }
        finally
        {
            if (qualificationSession is not null)
            {
                try
                {
                    await qualificationSession.CompleteAsync(request, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    diagnostics.Add($"ARCH7B_LEASE_RELEASE_FAILURE:{exception.GetType().Name}");
                }
            }
        }

        LmaxFixArch7bKnownOrderResult ResultOutside(string status, string? resultBlocker)
            => new(
                "fix-arch7b-known-order-lifecycle",
                status,
                connected,
                loggedOn,
                openingSent,
                cancelSent,
                flattenSent,
                statusRequestCount,
                logoutSent,
                resultBlocker,
                reports,
                diagnostics,
                startedAt,
                DateTimeOffset.UtcNow);
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value))).ToLowerInvariant();
}
