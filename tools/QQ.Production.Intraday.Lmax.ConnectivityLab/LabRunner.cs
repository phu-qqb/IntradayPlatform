namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class LmaxConnectivityLabRunner(
    ILmaxPublicDataClient publicDataClient,
    ILmaxAccountApiClient accountClient,
    ILmaxFixSessionClient fixClient,
    LmaxConnectivityLabSafetyValidator safety)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var command = args.FirstOrDefault(x => !x.StartsWith('-')) ?? "print-config";
        var optionArgs = args.Where(x => x.StartsWith('-')).ToArray();
        var options = LmaxConnectivityLabOptions.FromEnvironmentAndArgs(optionArgs);
        var explicitConfirm = args.Any(x => x.Equals("--confirm-demo-order", StringComparison.OrdinalIgnoreCase));

        if (command.Equals("fix-capabilities", StringComparison.OrdinalIgnoreCase))
        {
            var capabilities = LmaxFixRecoveryCodec.ScanDefaultDictionary();
            WriteCapabilitiesResult(capabilities);
            return capabilities.Status == "Failed" ? 1 : 0;
        }

        if (command.Equals("fix-trade-capture-smoke", StringComparison.OrdinalIgnoreCase))
        {
            var tradeCaptureOptions = LmaxFixTradeCaptureRequestOptions.From(
                DateTimeOffset.UtcNow,
                GetIntArg(optionArgs, "lookback-minutes", 1440),
                GetDateTimeOffsetArg(optionArgs, "start-utc"),
                GetDateTimeOffsetArg(optionArgs, "end-utc"),
                GetStringArg(optionArgs, "account"),
                GetIntArg(optionArgs, "max-wait-seconds", 10),
                GetIntArg(optionArgs, "max-reports", 50),
                HasFlag(optionArgs, "show-fix-messages"));
            var tradeCaptureResult = await fixClient.TradeCaptureSmokeAsync(options, tradeCaptureOptions, cancellationToken);
            WriteTradeCaptureResult(tradeCaptureResult);
            return tradeCaptureResult.Status == "Failed" ? 1 : 0;
        }

        if (command.Equals("fix-trade-capture-replay", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = ReplayTradeCaptureFixture(options, GetStringArg(optionArgs, "fixture"));
            return exitCode;
        }

        if (command.Equals("fix-execution-report-replay", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = ReplayExecutionReportFixture(options, GetStringArg(optionArgs, "fixture"));
            return exitCode;
        }

        if (command.Equals("fix-demo-order-lifecycle", StringComparison.OrdinalIgnoreCase))
        {
            var request = LmaxFixDemoOrderRequest.From(
                options,
                GetStringArg(optionArgs, "side"),
                GetStringArg(optionArgs, "order-type"),
                GetStringArg(optionArgs, "time-in-force"),
                GetDecimalArg(optionArgs, "venue-quantity"),
                GetDecimalArg(optionArgs, "limit-price"),
                GetDecimalArg(optionArgs, "max-notional-usd"),
                GetStringArg(optionArgs, "client-order-id"),
                GetStringArg(optionArgs, "account"),
                HasFlag(optionArgs, "confirm-demo-order") || explicitConfirm,
                GetBoolArg(optionArgs, "dry-run", options.DryRun),
                GetIntArg(optionArgs, "max-wait-seconds", options.RequestTimeoutSeconds),
                HasFlag(optionArgs, "show-fix-messages")) with
                {
                    IncludeHandlInst = HasFlag(optionArgs, "include-handl-inst")
                };
            var lifecycleResult = await fixClient.DemoOrderLifecycleAsync(options, request, explicitConfirm || HasFlag(optionArgs, "confirm-demo-order"), cancellationToken);
            WriteDemoOrderLifecycleResult(lifecycleResult);
            return lifecycleResult.Status == "Failed" ? 1 : lifecycleResult.Status == "Skipped" ? 2 : 0;
        }

        if (command.Equals("fix-order-status-dry-run", StringComparison.OrdinalIgnoreCase))
        {
            var dryRunResult = BuildOrderStatusDryRun(options, optionArgs);
            WriteResult(dryRunResult);
            return dryRunResult.Status == "Blocked" ? 2 : 0;
        }

        if (command.Equals("fix-order-status-smoke", StringComparison.OrdinalIgnoreCase))
        {
            var orderStatusResult = await fixClient.OrderStatusSmokeAsync(
                options,
                new LmaxFixOrderStatusSmokeRequest(
                    GetStringArg(optionArgs, "cl-ord-id"),
                    GetStringArg(optionArgs, "account"),
                    GetStringArg(optionArgs, "security-id") ?? GetStringArg(optionArgs, "lmax-instrument-id"),
                    GetStringArg(optionArgs, "security-id-source"),
                    MapSideArg(GetStringArg(optionArgs, "side")),
                    GetStringArg(optionArgs, "ord-status-req-id"),
                    GetIntArg(optionArgs, "max-wait-seconds", 10),
                    HasFlag(optionArgs, "show-fix-messages")),
                cancellationToken);
            WriteOrderStatusResult(orderStatusResult);
            return orderStatusResult.Status == "Failed" ? 1 : orderStatusResult.Status == "Skipped" ? 2 : 0;
        }

        if (command.Equals("fix-order-mass-status-smoke", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("fix-position-report-smoke", StringComparison.OrdinalIgnoreCase))
        {
            var skippedResult = UnsupportedReadOnlyFixCommand(command);
            WriteResult(skippedResult);
            return 0;
        }

        if (command.Equals("fix-marketdata-snapshot-smoke", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("fix-market-data-snapshot-smoke", StringComparison.OrdinalIgnoreCase))
        {
            var marketDataResult = await fixClient.MarketDataSnapshotSmokeAsync(options, cancellationToken);
            WriteMarketDataResult(marketDataResult);
            return marketDataResult.Status == "Failed" ? 1 : 0;
        }

        if (command.StartsWith("account-api-", StringComparison.OrdinalIgnoreCase))
        {
            var accountResult = command.ToLowerInvariant() switch
            {
                "account-api-config-check" => ToAccountResult(accountClient.CheckConfig(options), options),
                "account-api-discover" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.DefaultDiscoveryEndpoints, HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                "account-api-smoke" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.DefaultDiscoveryEndpoints.Take(8).ToList(), HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                "account-api-positions-smoke" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.PositionEndpoints, HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                "account-api-balances-smoke" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.BalanceEndpoints, HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                "account-api-open-orders-smoke" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.OpenOrderEndpoints, HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                "account-api-trade-history-smoke" => await accountClient.DiscoverAsync(options, command, LmaxAccountApiClient.TradeHistoryEndpoints, HasFlag(optionArgs, "show-response-excerpt"), cancellationToken),
                _ => LmaxAccountApiSmokeResult.Skipped(command, $"Unknown account API command '{command}'.", options, [])
            };
            WriteAccountApiResult(accountResult);
            return accountResult.Status == "Failed" ? 1 : 0;
        }

        var result = command.ToLowerInvariant() switch
        {
            "print-config" => PrintConfig(options),
            "check-public-data-config" => CheckPublicDataConfig(options),
            "public-data-smoke" => await publicDataClient.SmokeAsync(options, cancellationToken),
            "fix-session-dry-run" => fixClient.Validate(options, marketData: false),
            "fix-market-data-smoke" => await fixClient.SmokeAsync(options, marketData: true, cancellationToken),
            "fix-order-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: false, cancellationToken),
            "fix-marketdata-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: true, cancellationToken),
            "fix-market-data-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: true, cancellationToken),
            "order-lifecycle-demo-dry-run" => OrderLifecycleDryRun(options),
            "order-lifecycle-demo" => OrderLifecycleDemo(options, explicitConfirm),
            _ => LabCommandResult.Blocked(command, $"Unknown command '{command}'.", [])
        };

        WriteResult(result);
        return result.Status == "Blocked" ? 2 : 0;
    }

    public LabCommandResult PrintConfig(LmaxConnectivityLabOptions options)
    {
        foreach (var item in options.ToSafeDictionary())
        {
            Console.WriteLine($"{item.Key}: {item.Value}");
        }

        return LabCommandResult.Ok("print-config", "Printed safe masked configuration. No network calls were made.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
    }

    public LabCommandResult CheckPublicDataConfig(LmaxConnectivityLabOptions options)
    {
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList();
        if (string.IsNullOrWhiteSpace(options.PublicDataApiBaseUrl)) return LabCommandResult.Skipped("check-public-data-config", "Public data API base URL is not configured.", decisions);
        if (string.IsNullOrWhiteSpace(options.InstrumentSymbol) || string.IsNullOrWhiteSpace(options.LmaxInstrumentId)) return LabCommandResult.Skipped("check-public-data-config", "Instrument symbol or LMAX instrument id is not configured.", decisions);
        return LabCommandResult.Ok("check-public-data-config", $"Configured mapping {options.InstrumentSymbol} -> {options.LmaxInstrumentId}. No network call was made.", decisions);
    }

    public LabCommandResult OrderLifecycleDryRun(LmaxConnectivityLabOptions options)
    {
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).Concat([
            "Demo order request would use a tiny notional and demo/UAT only.",
            "No order was submitted.",
            "Required gates for real demo submission: AllowExternalConnections=true, AllowOrderSubmission=true, AllowLiveTrading=false, DryRun=false, EnvironmentName Demo/UAT, --confirm-demo-order."
        ]).ToList();
        return LabCommandResult.Ok("order-lifecycle-demo-dry-run", $"Constructed dry-run order request for {options.InstrumentSymbol}/{options.LmaxInstrumentId}. No network call was made.", decisions);
    }

    public LabCommandResult OrderLifecycleDemo(LmaxConnectivityLabOptions options, bool explicitConfirmation)
    {
        var issues = safety.ValidateForOrderSubmission(options, explicitConfirmation);
        if (issues.Count > 0) return LabCommandResult.Blocked("order-lifecycle-demo", string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).Concat(issues).ToList());
        return LabCommandResult.Skipped("order-lifecycle-demo", "Safety gates passed for demo/UAT only, but no real LMAX order submission implementation is wired into the lab yet.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
    }

    private static LabCommandResult BuildOrderStatusDryRun(LmaxConnectivityLabOptions options, string[] args)
    {
        var clOrdId = GetStringArg(args, "cl-ord-id") ?? "DRYRUN-CLORDID";
        var target = options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "LMXBD";
        var sender = string.IsNullOrWhiteSpace(options.FixSenderCompId) ? "DRYRUN-SENDER" : options.FixSenderCompId!;
        var message = LmaxFixRecoveryCodec.BuildOrderStatusRequestDryRun(
            sender,
            target,
            sequenceNumber: 2,
            new LmaxFixOrderStatusRequest(
                clOrdId,
                GetStringArg(args, "account"),
                GetStringArg(args, "security-id"),
                GetStringArg(args, "security-id-source"),
                GetStringArg(args, "side"),
                GetStringArg(args, "ord-status-req-id")));
        if (message.Status == "Blocked")
        {
            return LabCommandResult.Blocked("fix-order-status-dry-run", message.Message, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).Concat(message.Warnings).ToList());
        }

        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options)
            .Concat([
                "Built read-only OrderStatusRequest 35=H.",
                "No network call was made.",
                "No order was submitted.",
                $"FIX: {message.FixMessageSanitized}"
            ])
            .Concat(message.Warnings)
            .ToList();
        return LabCommandResult.Ok("fix-order-status-dry-run", $"Built OrderStatusRequest dry-run for ClOrdID={clOrdId}.", decisions);
    }

    private static LabCommandResult UnsupportedReadOnlyFixCommand(string command)
    {
        var scan = LmaxFixRecoveryCodec.ScanDefaultDictionary();
        var expected = command.ToLowerInvariant() switch
        {
            "fix-order-mass-status-smoke" => "OrderMassStatusRequest",
            "fix-position-report-smoke" => "RequestForPositions",
            _ => "OrderStatusRequest"
        };
        var capability = scan.Capabilities.FirstOrDefault(x => x.MessageName.Equals(expected, StringComparison.OrdinalIgnoreCase));
        if (scan.Status == "Skipped")
        {
            return LabCommandResult.Skipped(command, $"{scan.Message} Cannot confirm {expected} support. No network call was made.", []);
        }

        if (capability is null || !capability.Supported)
        {
            return LabCommandResult.Skipped(command, $"{expected} is unsupported by the available LMAX FIX trading dictionary. No network call was made.", []);
        }

        return LabCommandResult.Skipped(command, $"{expected} appears in the dictionary, but this read-only smoke is not implemented yet. No network call was made.", []);
    }

    private static int ReplayTradeCaptureFixture(LmaxConnectivityLabOptions options, string? fixturePath)
    {
        var path = string.IsNullOrWhiteSpace(fixturePath) ? DefaultTradeCaptureReplayFixturePath() : Path.GetFullPath(fixturePath);
        Console.WriteLine("Command: fix-trade-capture-replay");
        Console.WriteLine("Status: Running");
        Console.WriteLine("ExternalConnections: False");
        Console.WriteLine($"FixturePath: {path}");
        if (!File.Exists(path))
        {
            Console.WriteLine("Status: Skipped");
            Console.WriteLine("Message: Synthetic trade capture replay fixture was not found. No network call was made.");
            return 0;
        }

        var messages = File.ReadLines(path)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'))
            .Select(NormalizeReplayFixLine)
            .ToList();
        var normalized = messages.Select(x => LmaxFixRecoveryCodec.NormalizeTradeCaptureReport(x, options)).ToList();
        Console.WriteLine("Status: Ok");
        Console.WriteLine($"MessageCount: {messages.Count}");
        foreach (var item in normalized)
        {
            var report = item.Report;
            var eod = item.EodShape;
            Console.WriteLine($"Report: ExecID={report.ExecId} SecurityID={report.SecurityId} Symbol={report.Symbol} InternalSymbol={report.InternalSymbol} Side={report.NormalizedSide?.ToString() ?? report.Side} LastQty={report.LastQty} LastPx={report.LastPx} TradeDate={report.TradeDate} TransactTimeUtc={report.TransactTime:O} Account={report.Account} LastReportRequested={report.LastReportRequested} CanMapToEodIndividualTrade={report.CanMapToEodIndividualTrade}");
            Console.WriteLine($"EodShape: ExecutionId={eod.ExecutionId} MtfExecutionId={eod.MtfExecutionId} TimestampUtc={eod.TimestampUtc:O} TradeQuantity={eod.TradeQuantity} TradePrice={eod.TradePrice} InstrumentId={eod.InstrumentId} Symbol={eod.Symbol} InstructionId={eod.InstructionId} OrderId={eod.OrderId} AccountId={eod.AccountId} UnitsBoughtSold={eod.UnitsBoughtSold} NotionalValue={eod.NotionalValue}");
            if (item.MissingForEodComparison.Count > 0) Console.WriteLine($"MissingForEodComparison: {string.Join("; ", item.MissingForEodComparison)}");
            if (item.Warnings.Count > 0) Console.WriteLine($"Warnings: {string.Join("; ", item.Warnings)}");
        }

        Console.WriteLine("Message: Synthetic AE replay completed. No network call was made and no data was persisted.");
        return 0;
    }

    private static int ReplayExecutionReportFixture(LmaxConnectivityLabOptions options, string? fixturePath)
    {
        var path = string.IsNullOrWhiteSpace(fixturePath) ? DefaultExecutionReportReplayFixturePath() : Path.GetFullPath(fixturePath);
        Console.WriteLine("Command: fix-execution-report-replay");
        Console.WriteLine("Status: Running");
        Console.WriteLine("ExternalConnections: False");
        Console.WriteLine($"FixturePath: {path}");
        if (!File.Exists(path))
        {
            Console.WriteLine("Status: Skipped");
            Console.WriteLine("Message: Synthetic execution report replay fixture was not found. No network call was made.");
            return 0;
        }

        var messages = File.ReadLines(path)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'))
            .Select(NormalizeReplayFixLine)
            .ToList();
        var normalized = messages.Select(x => LmaxFixRecoveryCodec.NormalizeExecutionReport(x, options)).ToList();
        Console.WriteLine("Status: Ok");
        Console.WriteLine($"MessageCount: {messages.Count}");
        foreach (var item in normalized)
        {
            var report = item.Report;
            var orderEvent = item.InternalEvent;
            Console.WriteLine($"Report: ExecID={report.ExecId} OrderID={report.OrderId} ClOrdID={report.ClOrdId} ExecType={report.ExecType} OrdStatus={report.OrdStatus} SecurityID={report.SecurityId} Symbol={report.Symbol} InternalSymbol={report.InternalSymbol} Side={report.Side?.ToString() ?? report.SideRaw} OrderQty={report.OrderQty} LeavesQty={report.LeavesQty} CumQty={report.CumQty} LastQty={report.LastQty} LastPx={report.LastPx} AvgPx={report.AvgPx} Price={report.Price} OrdType={report.OrdType} TimeInForce={report.TimeInForce} TransactTimeUtc={report.TransactTimeUtc:O} Account={report.Account} CanMapToInternalOrderEvent={report.CanMapToInternalOrderEvent}");
            Console.WriteLine($"InternalEvent: EventType={orderEvent.EventType} ExecID={orderEvent.ExecId} OrderID={orderEvent.OrderId} ClOrdID={orderEvent.ClOrdId} Symbol={orderEvent.InternalSymbol} Side={orderEvent.Side} LastQty={orderEvent.LastQty} LastPx={orderEvent.LastPx} CumQty={orderEvent.CumQty} LeavesQty={orderEvent.LeavesQty} TransactTimeUtc={orderEvent.TransactTimeUtc:O} Message={orderEvent.Message}");
            if (item.MissingForInternalOrderEvent.Count > 0) Console.WriteLine($"MissingForInternalOrderEvent: {string.Join("; ", item.MissingForInternalOrderEvent)}");
            if (item.Warnings.Count > 0) Console.WriteLine($"Warnings: {string.Join("; ", item.Warnings)}");
        }

        Console.WriteLine("Message: Synthetic ExecutionReport replay completed. No network call was made and no data was persisted.");
        return 0;
    }

    private static string NormalizeReplayFixLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Contains('|', StringComparison.Ordinal)) return trimmed.Replace('|', LmaxFixMarketDataCodec.Soh);
        return trimmed;
    }

    private static string DefaultTradeCaptureReplayFixturePath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "synthetic-trade-capture-ae.fix"));

    private static string DefaultExecutionReportReplayFixturePath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "synthetic-execution-report-35-8.fix"));

    private static void WriteResult(LabCommandResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        if (result.SessionType is not null) Console.WriteLine($"SessionType: {result.SessionType}");
        if (result.Connected is not null) Console.WriteLine($"Connected: {result.Connected}");
        if (result.LoggedOn is not null) Console.WriteLine($"LoggedOn: {result.LoggedOn}");
        if (result.StartedAtUtc is not null) Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        if (result.CompletedAtUtc is not null) Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static void WriteAccountApiResult(LmaxAccountApiSmokeResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"BaseUrl: {result.BaseUrl}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var attempt in result.AuthAttempts)
        {
            Console.WriteLine($"AuthAttempt: Mode={attempt.AuthMode} Status={attempt.Status} Message={attempt.Message}");
        }
        foreach (var probe in result.EndpointProbes)
        {
            Console.WriteLine($"Probe: Endpoint={probe.Endpoint} AuthMode={probe.AuthMode} Status={probe.Status} HttpStatus={probe.HttpStatus} ContentType={probe.ContentType} ItemCount={probe.ItemCount} Fields={string.Join(",", probe.TopLevelFields)} Message={probe.Message}");
            if (!string.IsNullOrWhiteSpace(probe.Excerpt)) Console.WriteLine($"Excerpt: {probe.Excerpt}");
        }
        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static LmaxAccountApiSmokeResult ToAccountResult(LabCommandResult result, LmaxConnectivityLabOptions options)
    {
        var now = DateTimeOffset.UtcNow;
        return new(result.Command, result.Status, result.Message, options.AccountApiBaseUrl ?? "(not configured)", result.SafetyDecisions, [], [], now, now);
    }

    private static bool HasFlag(IEnumerable<string> args, string name)
        => args.Any(x => x.Equals($"--{name}=true", StringComparison.OrdinalIgnoreCase) || x.Equals($"--{name}", StringComparison.OrdinalIgnoreCase));

    private static string? GetStringArg(IEnumerable<string> args, string name)
    {
        var prefix = $"--{name}=";
        return args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
    }

    private static int GetIntArg(IEnumerable<string> args, string name, int defaultValue)
        => int.TryParse(GetStringArg(args, name), out var parsed) ? parsed : defaultValue;

    private static decimal? GetDecimalArg(IEnumerable<string> args, string name)
        => decimal.TryParse(GetStringArg(args, name), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool GetBoolArg(IEnumerable<string> args, string name, bool defaultValue)
        => bool.TryParse(GetStringArg(args, name), out var parsed) ? parsed : defaultValue;

    private static DateTimeOffset? GetDateTimeOffsetArg(IEnumerable<string> args, string name)
        => DateTimeOffset.TryParse(GetStringArg(args, name), out var parsed) ? parsed.ToUniversalTime() : null;

    private static string? MapSideArg(string? side)
        => side?.Equals("Buy", StringComparison.OrdinalIgnoreCase) == true
            ? "1"
            : side?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true
                ? "2"
                : side;

    private static void WriteCapabilitiesResult(LmaxFixCapabilityScanResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        if (!string.IsNullOrWhiteSpace(result.DictionaryPath)) Console.WriteLine($"DictionaryPath: {result.DictionaryPath}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var capability in result.Capabilities)
        {
            Console.WriteLine($"Capability: MessageName={capability.MessageName} MsgType={capability.MsgType} Supported={capability.Supported} Required=[{string.Join(",", capability.RequiredFields)}] Optional=[{string.Join(",", capability.OptionalFields)}]");
        }
    }

    private static void WriteTradeCaptureResult(LmaxFixTradeCaptureSmokeResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Connected: {result.Connected}");
        Console.WriteLine($"LoggedOn: {result.LoggedOn}");
        Console.WriteLine($"RequestSent: {result.RequestSent}");
        Console.WriteLine($"AckReceived: {result.AckReceived}");
        Console.WriteLine($"AckAccepted: {result.AckAccepted}");
        Console.WriteLine($"RequestRejected: {result.RequestRejected}");
        if (!string.IsNullOrWhiteSpace(result.AckRejectText)) Console.WriteLine($"AckRejectText: {result.AckRejectText}");
        if (!string.IsNullOrWhiteSpace(result.RejectMsgType)) Console.WriteLine($"RejectMsgType: {result.RejectMsgType}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefTagId)) Console.WriteLine($"RejectRefTagId: {result.RejectRefTagId}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefMsgType)) Console.WriteLine($"RejectRefMsgType: {result.RejectRefMsgType}");
        if (!string.IsNullOrWhiteSpace(result.RejectReasonCode)) Console.WriteLine($"RejectReasonCode: {result.RejectReasonCode}");
        if (!string.IsNullOrWhiteSpace(result.RejectText)) Console.WriteLine($"RejectText: {result.RejectText}");
        if (!string.IsNullOrWhiteSpace(result.LastReceivedMsgType)) Console.WriteLine($"LastReceivedMsgType: {result.LastReceivedMsgType}");
        if (result.ExpectedTradeReportCount is not null) Console.WriteLine($"ExpectedTradeReportCount: {result.ExpectedTradeReportCount}");
        Console.WriteLine($"NoMoreReports: {result.NoMoreReports}");
        Console.WriteLine($"LogoutSent: {result.LogoutSent}");
        Console.WriteLine($"TradeReportCount: {result.TradeReportCount}");
        Console.WriteLine($"LastReportRequested: {result.LastReportRequested}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var report in result.Reports)
        {
            Console.WriteLine($"Report: ExecID={report.ExecId} SecurityID={report.SecurityId} Symbol={report.Symbol} InternalSymbol={report.InternalSymbol} Side={report.NormalizedSide?.ToString() ?? report.Side} LastQty={report.LastQty} LastPx={report.LastPx} TradeDate={report.TradeDate} TransactTimeUtc={report.TransactTime:O} Account={report.Account} LastReportRequested={report.LastReportRequested} CanMapToEodIndividualTrade={report.CanMapToEodIndividualTrade}");
            if (report.MissingForEodComparison is { Count: > 0 }) Console.WriteLine($"ReportMissingForEodComparison: {string.Join("; ", report.MissingForEodComparison)}");
            if (report.Warnings is { Count: > 0 }) Console.WriteLine($"ReportWarnings: {string.Join("; ", report.Warnings)}");
        }
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }
        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static void WriteMarketDataResult(LmaxFixMarketDataSmokeResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Connected: {result.Connected}");
        Console.WriteLine($"LoggedOn: {result.LoggedOn}");
        Console.WriteLine($"TcpConnected: {result.TcpConnected}");
        Console.WriteLine($"TlsHandshakeCompleted: {result.TlsHandshakeCompleted}");
        Console.WriteLine($"FixLogonSent: {result.FixLogonSent}");
        Console.WriteLine($"FixLoggedOn: {result.FixLoggedOn}");
        Console.WriteLine($"RequestSent: {result.RequestSent}");
        Console.WriteLine($"MarketDataRequestSent: {result.MarketDataRequestSent}");
        Console.WriteLine($"MarketDataSnapshotReceived: {result.MarketDataSnapshotReceived}");
        Console.WriteLine($"RequestRejected: {result.RequestRejected}");
        Console.WriteLine($"MarketDataRejectReceived: {result.MarketDataRejectReceived}");
        Console.WriteLine($"LogoutSent: {result.LogoutSent}");
        if (!string.IsNullOrWhiteSpace(result.RejectReason)) Console.WriteLine($"RejectReason: {result.RejectReason}");
        if (!string.IsNullOrWhiteSpace(result.RejectText)) Console.WriteLine($"RejectText: {result.RejectText}");
        if (!string.IsNullOrWhiteSpace(result.LastReceivedMsgType)) Console.WriteLine($"LastReceivedMsgType: {result.LastReceivedMsgType}");
        Console.WriteLine($"MessageCount: {result.MessageCount}");
        if (result.BestBid is not null) Console.WriteLine($"BestBid: {result.BestBid}");
        if (result.BestAsk is not null) Console.WriteLine($"BestAsk: {result.BestAsk}");
        if (result.Mid is not null) Console.WriteLine($"Mid: {result.Mid}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var entry in result.Entries)
        {
            Console.WriteLine($"Entry: Type={entry.EntryType} Price={entry.Price} Size={entry.Size} Symbol={entry.Symbol} SecurityId={entry.SecurityId} UpdateAction={entry.UpdateAction}");
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }

        foreach (var attempt in result.Attempts)
        {
            Console.WriteLine($"Attempt: {attempt}");
        }

        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static void WriteDemoOrderLifecycleResult(LmaxFixDemoOrderLifecycleResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Connected: {result.Connected}");
        Console.WriteLine($"LoggedOn: {result.LoggedOn}");
        Console.WriteLine($"OrderSent: {result.OrderSent}");
        Console.WriteLine($"ExecutionReportReceived: {result.ExecutionReportReceived}");
        Console.WriteLine($"TerminalExecutionReportReceived: {result.TerminalExecutionReportReceived}");
        Console.WriteLine($"RequestRejected: {result.RequestRejected}");
        if (!string.IsNullOrWhiteSpace(result.RejectMsgType)) Console.WriteLine($"RejectMsgType: {result.RejectMsgType}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefTagId)) Console.WriteLine($"RejectRefTagId: {result.RejectRefTagId}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefMsgType)) Console.WriteLine($"RejectRefMsgType: {result.RejectRefMsgType}");
        if (!string.IsNullOrWhiteSpace(result.RejectReasonCode)) Console.WriteLine($"RejectReasonCode: {result.RejectReasonCode}");
        if (!string.IsNullOrWhiteSpace(result.RejectText)) Console.WriteLine($"RejectText: {result.RejectText}");
        if (!string.IsNullOrWhiteSpace(result.FinalStatus)) Console.WriteLine($"FinalStatus: {result.FinalStatus}");
        Console.WriteLine($"LogoutSent: {result.LogoutSent}");
        if (!string.IsNullOrWhiteSpace(result.ClientOrderId)) Console.WriteLine($"ClientOrderId: {result.ClientOrderId}");
        if (!string.IsNullOrWhiteSpace(result.LastReceivedMsgType)) Console.WriteLine($"LastReceivedMsgType: {result.LastReceivedMsgType}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var report in result.ExecutionReports)
        {
            Console.WriteLine($"ExecutionReport: ExecID={report.ExecId} OrderID={report.OrderId} ClOrdID={report.ClOrdId} ExecType={report.ExecType} OrdStatus={report.OrdStatus} Symbol={report.InternalSymbol ?? report.Symbol} Side={report.Side} LastQty={report.LastQty} LastPx={report.LastPx} CumQty={report.CumQty} LeavesQty={report.LeavesQty} Text={report.Text}");
        }

        foreach (var decision in result.DemoSafetyDecisions)
        {
            Console.WriteLine($"SafetyGate: {decision.Gate} Passed={decision.Passed} Message={decision.Message}");
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }

        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static void WriteOrderStatusResult(LmaxFixOrderStatusSmokeResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Connected: {result.Connected}");
        Console.WriteLine($"LoggedOn: {result.LoggedOn}");
        Console.WriteLine($"RequestSent: {result.RequestSent}");
        Console.WriteLine($"ExecutionReportReceived: {result.ExecutionReportReceived}");
        Console.WriteLine($"RequestRejected: {result.RequestRejected}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefTagId)) Console.WriteLine($"RejectRefTagId: {result.RejectRefTagId}");
        if (!string.IsNullOrWhiteSpace(result.RejectRefMsgType)) Console.WriteLine($"RejectRefMsgType: {result.RejectRefMsgType}");
        if (!string.IsNullOrWhiteSpace(result.RejectText)) Console.WriteLine($"RejectText: {result.RejectText}");
        if (!string.IsNullOrWhiteSpace(result.ClOrdId)) Console.WriteLine($"ClOrdID: {result.ClOrdId}");
        if (!string.IsNullOrWhiteSpace(result.BrokerOrderId)) Console.WriteLine($"BrokerOrderID: {result.BrokerOrderId}");
        if (!string.IsNullOrWhiteSpace(result.FinalOrdStatus)) Console.WriteLine($"FinalOrdStatus: {result.FinalOrdStatus}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"LogoutSent: {result.LogoutSent}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var report in result.ExecutionReports)
        {
            Console.WriteLine($"ExecutionReport: ExecID={report.ExecId} OrderID={report.OrderId} ClOrdID={report.ClOrdId} ExecType={report.ExecType} OrdStatus={report.OrdStatus} SecurityID={report.SecurityId} Symbol={report.InternalSymbol ?? report.Symbol} Side={report.Side} OrderQty={report.OrderQty} CumQty={report.CumQty} LeavesQty={report.LeavesQty} LastQty={report.LastQty} LastPx={report.LastPx} AvgPx={report.AvgPx} TransactTimeUtc={report.TransactTimeUtc:O} Text={report.Text}");
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }

        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }
}
