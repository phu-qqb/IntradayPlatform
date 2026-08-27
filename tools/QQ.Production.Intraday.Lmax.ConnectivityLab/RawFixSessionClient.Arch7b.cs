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

        if (request.Activation is LmaxFixArch7bActivation.DryRun or LmaxFixArch7bActivation.ProductionDryRun)
        {
            if (request.Activation == LmaxFixArch7bActivation.ProductionDryRun)
            {
                var dryRunBlockers = LmaxFixArch7bKnownOrderContract.Validate(options, request, startedAt).ToList();
                if (dryRunBlockers.Count != 0)
                    return LmaxFixArch7bKnownOrderResult.Skipped(dryRunBlockers[0], dryRunBlockers);
            }
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
        var profile = request.ExecutionProfile;

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
        TcpClient? orderEntryTcp = null;
        Stream? orderEntryStream = null;

        try
        {
            await qualificationSession!.InitializeAsync(request, cancellationToken);
            recovery = await qualificationSession.LoadRecoveryStateAsync(
                request,
                cancellationToken);
            statusRequestCount = recovery.OrderStatusRequestCount;
            recoveryPlan = LmaxFixArch7bRecoveryPlanner.Build(recovery);
            var openingMarketObservationId = recovery.OpeningMarketObservationId ??
                request.OpeningMarketObservationId;
            string? flattenMarketObservationId = recovery.FlattenMarketObservationId;

            if (request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce &&
                recoveryPlan.MaySendOpeningNewOrderSingle)
            {
                var preflightStartedAtUtc = DateTimeOffset.UtcNow;
                var preflightOptions = CreateArch7bReadOnlyMarketDataOptions(options, profile);
                var remaining = request.DeadlineUtc - preflightStartedAtUtc;
                if (remaining <= TimeSpan.Zero)
                    return await ResultWithCleanupAsync("Failed", "ARCH7B_DEADLINE_EXCEEDED");
                var preflightBudget = TimeSpan.FromSeconds(profile.MaximumBboAgeSeconds);
                using var preflightDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                preflightDeadline.CancelAfter(remaining < preflightBudget ? remaining : preflightBudget);
                var preflightMarketData = await Arch7bProductionReadOnlyMarketDataSnapshotAsync(
                    preflightOptions,
                    request,
                    request.DeadlineUtc,
                    preflightDeadline.Token);
                var preflightDecision = LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
                    preflightOptions,
                    preflightMarketData,
                    preflightStartedAtUtc,
                    DateTimeOffset.UtcNow,
                    request.OpeningMarketObservationId,
                    profile);
                diagnostics.Add($"ARCH7B_PRODUCTION_PREFLIGHT_MARKET_DATA:{preflightMarketData.Status}");
                if (!preflightDecision.Allowed)
                {
                    diagnostics.AddRange(preflightDecision.Blockers);
                    return await ResultWithCleanupAsync(
                        "Failed",
                        "ARCH7B_PRODUCTION_PREFLIGHT_MARKET_DATA_UNAVAILABLE");
                }

                var postPreflightBlockers = LmaxFixArch7bKnownOrderContract.Validate(
                    options, request, DateTimeOffset.UtcNow).ToList();
                if (postPreflightBlockers.Count != 0)
                    return await ResultWithCleanupAsync("Failed", postPreflightBlockers[0]);
            }

            await arch7bObserver!.RecordSessionEventAsync(
                request,
                "KILL_SWITCH_ARMED_BEFORE_FIX_LOGON",
                null,
                startedAt,
                cancellationToken);

            orderEntryTcp = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                await orderEntryTcp.ConnectAsync(options.FixOrderHost!, options.FixOrderPort!.Value, connectTimeout.Token);
                connected = true;
            }

            Stream rawStream;
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
                rawStream = options.UseTls
                    ? await CreateTlsStreamAsync(orderEntryTcp, options.FixOrderHost!, connectTimeout.Token)
                    : orderEntryTcp.GetStream();

            orderEntryStream = rawStream;
            var stream = rawStream;
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
                return ResultOutside("Failed", "ARCH7B_FIX_LOGON_NOT_CONFIRMED");

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
                var preOpeningBlockers = LmaxFixArch7bKnownOrderContract.Validate(
                    options, request, DateTimeOffset.UtcNow).ToList();
                if (preOpeningBlockers.Count != 0)
                    return await ResultWithCleanupAsync("Failed", preOpeningBlockers[0]);
                var openingRequest = LmaxFixArch7bKnownOrderContract.LimitRequest(
                    profile,
                    request.AccountId,
                    LmaxFixDemoOrderSide.Buy,
                    profile.VenueQuantity,
                    request.OpeningLimitPrice,
                    request.OpeningClientOrderId);
                var opening = LmaxFixRecoveryCodec.BuildNewOrderSingle(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    openingRequest,
                    request.OpeningClientOrderId,
                    profile.SecurityIdSource);
                await PersistThenSendAsync(
                    stream,
                    "OPEN",
                    "D",
                    request.OpeningClientOrderId,
                    null,
                    "BUY",
                    profile.VenueQuantity,
                    request.OpeningLimitPrice,
                    openingMarketObservationId,
                    opening,
                    sequenceNumber++,
                    cancellationToken);
                openingSent = true;
            }

            var openingCancelDeadline = request.OpeningCancelAtUtc < request.DeadlineUtc
                ? request.OpeningCancelAtUtc
                : request.DeadlineUtc;
            await ReadUntilAsync(
                stream,
                openingCancelDeadline,
                () => OpeningTerminal() || OpeningFilledQuantity() >= profile.VenueQuantity,
                cancellationToken);
            if (blocker is not null)
                return await ResultWithCleanupAsync("Failed", blocker);

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
                        profile.Symbol,
                        "1",
                        profile.VenueQuantity,
                        profile.SecurityId,
                        profile.SecurityIdSource);
                    await PersistThenSendAsync(
                        stream,
                        "OPEN_RESIDUAL_CANCEL",
                        "F",
                        request.CancelClientOrderId,
                        request.OpeningClientOrderId,
                        "BUY",
                        profile.VenueQuantity,
                        null,
                        openingMarketObservationId,
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
                return await ResultWithCleanupAsync("Failed", blocker);
            if (!OpeningTerminal() || OpeningLeavesQuantity() > 0m)
                return await ResultWithCleanupAsync("Failed", "ARCH7B_OPENING_TERMINAL_NOT_CONFIRMED");
            if (openingFilled == 0m)
                return await ResultWithCleanupAsync("Failed", "ARCH7B_OPENING_ORDER_NOT_FILLED");
            if (openingFilled > profile.VenueQuantity ||
                openingFilled % profile.QuantityIncrement != 0m)
                return await ResultWithCleanupAsync("Failed", "ARCH7B_OPENING_FILL_QUANTITY_OUT_OF_BOUNDS");

            var openingFillQuantity =
                await qualificationSession.ReadValidatedFillQuantityAsync(
                    request,
                    request.OpeningClientOrderId,
                    cancellationToken);
            if (Math.Abs(openingFillQuantity - openingFilled) > 0.00000001m)
            {
                await arch7bObserver.RecordSessionEventAsync(
                    request,
                    "KILL_SWITCH_ACTIVATED_OPENING_FILL_CUMQTY_DIVERGENCE",
                    lastInboundSequenceNumber,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                return await ResultWithCleanupAsync(
                    "Failed",
                    "ARCH7B_OPENING_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP");
            }

            if (!recoveryPlan.MaySendFlattenNewOrderSingle)
            {
                if (string.IsNullOrWhiteSpace(flattenMarketObservationId))
                    return await ResultWithCleanupAsync("Failed", "ARCH7B_RECOVERY_FLATTEN_MARKET_OBSERVATION_MISSING");
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
                var observationNotBeforeUtc = DateTimeOffset.UtcNow;
                var marketDataOnlyOptions = CreateArch7bReadOnlyMarketDataOptions(options, profile);
                LmaxFixArch7bMarketObservationDecision? marketDecision = null;
                for (var attempt = 1;
                     attempt <= Arch7bKnownOrderQualificationPolicy.MaximumFlattenBboAcquisitionAttempts &&
                     DateTimeOffset.UtcNow < request.DeadlineUtc;
                     attempt++)
                {
                    var attemptStartedAtUtc = DateTimeOffset.UtcNow;
                    var remaining = request.DeadlineUtc - attemptStartedAtUtc;
                    var attemptBudget = TimeSpan.FromSeconds(
                        profile.MaximumBboAgeSeconds);
                    using var acquisitionDeadline =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    acquisitionDeadline.CancelAfter(
                        remaining < attemptBudget ? remaining : attemptBudget);
                    var marketData = request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce
                        ? await Arch7bProductionReadOnlyMarketDataSnapshotAsync(
                            marketDataOnlyOptions,
                            request,
                            request.DeadlineUtc,
                            acquisitionDeadline.Token)
                        : await MarketDataSnapshotSmokeAsync(
                            marketDataOnlyOptions,
                            request.DeadlineUtc,
                            acquisitionDeadline.Token);
                    marketDecision =
                        LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
                            marketDataOnlyOptions,
                            marketData,
                            observationNotBeforeUtc,
                            DateTimeOffset.UtcNow,
                            openingMarketObservationId,
                            profile);
                    diagnostics.Add(
                        $"ARCH7B_FLATTEN_BBO_ATTEMPT_{attempt}:{marketData.Status}");
                    if (marketDecision.Allowed)
                        break;
                }

                if (marketDecision is null || !marketDecision.Allowed ||
                    marketDecision.Observation is null ||
                    marketDecision.LimitPrice is null)
                {
                    await arch7bObserver.RecordSessionEventAsync(
                        request,
                        "KILL_SWITCH_ACTIVATED_FLATTEN_BBO_UNAVAILABLE",
                        lastInboundSequenceNumber,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);
                    if (marketDecision is not null)
                        diagnostics.AddRange(marketDecision.Blockers);
                    return await ResultWithCleanupAsync(
                        "Failed",
                        marketDecision?.Blockers.FirstOrDefault() ??
                        "ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH");
                }

                flattenMarketObservationId = marketDecision.Observation.SnapshotSha256;
                var flattenLimitPrice = marketDecision.LimitPrice.Value;
                await arch7bObserver.RecordSessionEventAsync(
                    request,
                    "FLATTEN_MARKET_OBSERVATION_ACCEPTED",
                    lastInboundSequenceNumber,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                var flattenRequest = LmaxFixArch7bKnownOrderContract.LimitRequest(
                    profile,
                    request.AccountId,
                    LmaxFixDemoOrderSide.Sell,
                    openingFilled,
                    flattenLimitPrice,
                    request.FlattenClientOrderId);
                var flatten = LmaxFixRecoveryCodec.BuildNewOrderSingle(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    flattenRequest,
                    request.FlattenClientOrderId,
                    profile.SecurityIdSource);
                await PersistThenSendAsync(
                    stream,
                    "FLATTEN",
                    "D",
                    request.FlattenClientOrderId,
                    null,
                    "SELL",
                    openingFilled,
                    flattenLimitPrice,
                    flattenMarketObservationId,
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
                return await ResultWithCleanupAsync("Failed", blocker);
            if (!FlattenTerminal() || FlattenLeavesQuantity() > 0m || FlattenFilledQuantity() != openingFilled)
            {
                if (statusRequestCount < profile.MaximumOrderStatusRequestCount)
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
                return await ResultWithCleanupAsync("Failed", "ARCH7B_FLATTEN_NOT_CONFIRMED");

            var flattenFillQuantity =
                await qualificationSession.ReadValidatedFillQuantityAsync(
                    request,
                    request.FlattenClientOrderId,
                    cancellationToken);
            if (Math.Abs(flattenFillQuantity - FlattenFilledQuantity()) > 0.00000001m ||
                flattenFillQuantity > openingFillQuantity)
            {
                await arch7bObserver.RecordSessionEventAsync(
                    request,
                    "KILL_SWITCH_ACTIVATED_FLATTEN_FILL_CUMQTY_DIVERGENCE",
                    lastInboundSequenceNumber,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                return await ResultWithCleanupAsync(
                    "Failed",
                    "ARCH7B_FLATTEN_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP");
            }

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
                return await ResultWithCleanupAsync("Failed", "ARCH7B_FINAL_RECONCILIATION_NOT_FLAT");

            return await ResultWithCleanupAsync("Ok", null);

            async Task PersistThenSendAsync(
                Stream activeStream,
                string role,
                string messageType,
                string clientOrderId,
                string? originalClientOrderId,
                string side,
                decimal quantity,
                decimal? limitPrice,
                string marketObservationId,
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
                    marketObservationId,
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
                if (++statusRequestCount > profile.MaximumOrderStatusRequestCount)
                    throw new InvalidOperationException("ARCH7B_ORDER_STATUS_REQUEST_BUDGET_EXCEEDED");
                var status = LmaxFixRecoveryCodec.BuildOrderStatusRequest(
                    options.FixSenderCompId!,
                    target,
                    sequenceNumber,
                    clientOrderId,
                    request.AccountId,
                    profile.SecurityId,
                    profile.SecurityIdSource,
                    fixSide);
                var marketObservationId = clientOrderId == request.OpeningClientOrderId
                    ? openingMarketObservationId
                    : flattenMarketObservationId ??
                      throw new InvalidOperationException(
                          "ARCH7B_FLATTEN_MARKET_OBSERVATION_MISSING");
                await PersistThenSendAsync(
                    activeStream,
                    clientOrderId == request.OpeningClientOrderId ? "OPEN_STATUS" : "FLATTEN_STATUS",
                    "H",
                    clientOrderId,
                    null,
                    side,
                    0m,
                    null,
                    marketObservationId,
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
                if (report.Account != profile.AccountId)
                    return profile == Arch7bKnownOrderExecutionProfile.Demo
                        ? "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH"
                        : "ARCH7B_EXECUTION_REPORT_ACCOUNT_BINDING_MISMATCH";
                if (report.Symbol != profile.Symbol || report.SecurityId != profile.SecurityId)
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

        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await ResultWithCleanupAsync("Failed", "ARCH7B_LIFECYCLE_DEADLINE_EXCEEDED");
        }
        catch (Exception exception) when (
            exception is SocketException or IOException or AuthenticationException or
            ArgumentException or InvalidOperationException)
        {
            diagnostics.Add($"{exception.GetType().Name}:{exception.Message}");
            return await ResultWithCleanupAsync("Failed", $"ARCH7B_RUNNER_FAILURE:{exception.Message}");
        }
        finally
        {
            await EnsureOrderEntryLogoutAsync("ARCH7B_SCOPE_EXIT_CLEANUP");
            try
            {
                if (orderEntryStream is not null)
                    await orderEntryStream.DisposeAsync();
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_ORDER_ENTRY_STREAM_DISPOSE_FAILURE:{exception.GetType().Name}");
            }
            try
            {
                orderEntryTcp?.Dispose();
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_ORDER_ENTRY_TCP_DISPOSE_FAILURE:{exception.GetType().Name}");
            }
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

        async Task<LmaxFixArch7bKnownOrderResult> ResultWithCleanupAsync(
            string status,
            string? resultBlocker)
        {
            await EnsureOrderEntryLogoutAsync(
                resultBlocker ?? "ARCH7B_KNOWN_ORDER_LIFECYCLE_FLAT");
            return ResultOutside(status, resultBlocker);
        }

        async Task EnsureOrderEntryLogoutAsync(string reason)
        {
            if (!loggedOn || logoutSent || orderEntryStream is null)
                return;
            try
            {
                logoutSent = await TrySendLogoutAsync(
                    orderEntryStream,
                    options,
                    target,
                    sequenceNumber,
                    diagnostics,
                    reason);
                if (logoutSent && arch7bObserver is not null)
                    await arch7bObserver.RecordSessionEventAsync(
                        request,
                        "FIX_LOGOUT_SENT",
                        sequenceNumber,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_FAIL_CLOSED_LOGOUT_FAILURE:{exception.GetType().Name}");
            }
        }
    }

    private static LmaxConnectivityLabOptions CreateArch7bReadOnlyMarketDataOptions(
        LmaxConnectivityLabOptions options,
        Arch7bKnownOrderExecutionProfile profile)
    {
        var readOnly = CopyOptions(options);
        readOnly.AllowOrderSubmission = false;
        readOnly.AllowLiveTrading = false;
        readOnly.MarketDataRequestMode = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates;
        readOnly.MarketDepth = 1;
        readOnly.MarketDataMaxWaitSeconds = Math.Min(profile.MaximumBboAgeSeconds, 5);
        return readOnly;
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value))).ToLowerInvariant();
}
