using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        if (command.Equals("fix-readonly-evidence-capture", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("capture-readonly-evidence", StringComparison.OrdinalIgnoreCase))
        {
            var captureResult = await CaptureReadOnlyEvidenceAsync(
                options,
                GetIntArg(optionArgs, "trade-capture-lookback-minutes", GetIntArg(optionArgs, "lookback-minutes", 60)),
                GetIntArg(optionArgs, "max-reports", 20),
                GetStringArg(optionArgs, "output-directory") ?? Path.Combine("artifacts", "lmax-lab", "evidence"),
                GetStringArg(optionArgs, "cl-ord-id"),
                GetStringArg(optionArgs, "account"),
                GetIntArg(optionArgs, "max-wait-seconds", 10),
                HasFlag(optionArgs, "show-fix-messages"),
                cancellationToken);
            WriteReadOnlyEvidenceCaptureResult(captureResult);
            return captureResult.Status == "Failed" ? 1 : captureResult.Status == "Skipped" ? 2 : 0;
        }

        if (command.Equals("validate-evidence-file", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("validate-lmax-evidence-file", StringComparison.OrdinalIgnoreCase))
        {
            var path = GetStringArg(optionArgs, "evidence-file") ?? GetStringArg(optionArgs, "path");
            var writeNormalized = HasFlag(optionArgs, "write-normalized-copy");
            return ValidateEvidenceFile(path, writeNormalized);
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

        if (command.Equals("fix-demo-lifecycle-evidence", StringComparison.OrdinalIgnoreCase))
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
            var tradeCaptureOptions = LmaxFixTradeCaptureRequestOptions.From(
                DateTimeOffset.UtcNow,
                GetIntArg(optionArgs, "trade-capture-lookback-minutes", 1440),
                GetDateTimeOffsetArg(optionArgs, "start-utc"),
                GetDateTimeOffsetArg(optionArgs, "end-utc"),
                GetStringArg(optionArgs, "account"),
                GetIntArg(optionArgs, "max-wait-seconds", 10),
                GetIntArg(optionArgs, "max-reports", 50),
                HasFlag(optionArgs, "show-fix-messages"));
            var evidenceResult = await fixClient.DemoLifecycleEvidenceAsync(options, request, tradeCaptureOptions, explicitConfirm || HasFlag(optionArgs, "confirm-demo-order"), cancellationToken);
            WriteLifecycleEvidenceResult(evidenceResult);
            var outputJsonPath = GetStringArg(optionArgs, "output-json-path");
            if (!string.IsNullOrWhiteSpace(outputJsonPath))
            {
                WriteLifecycleEvidenceJson(outputJsonPath, evidenceResult);
            }
            return evidenceResult.Status == "Failed" ? 1 : evidenceResult.Status == "Skipped" ? 2 : 0;
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

    public async Task<LmaxFixReadOnlyEvidenceCaptureResult> CaptureReadOnlyEvidenceAsync(
        LmaxConnectivityLabOptions options,
        int tradeCaptureLookbackMinutes,
        int maxReports,
        string outputDirectory,
        string? clOrdId,
        string? account,
        int maxWaitSeconds,
        bool showFixMessages,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<string>
        {
            "CaptureMode=ReadOnly",
            $"Instrument={options.InstrumentSymbol}",
            $"LmaxInstrumentId={options.LmaxInstrumentId}",
            $"OrderStatusRequested={!string.IsNullOrWhiteSpace(clOrdId)}"
        };
        var safetyDecisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options)
            .Concat([
                "LAB ONLY: read-only evidence capture.",
                "No NewOrderSingle is built or sent.",
                "No order submission path is used.",
                "No data is persisted to the main trading database."
            ])
            .ToList();

        if (!options.AllowExternalConnections)
        {
            return new LmaxFixReadOnlyEvidenceCaptureResult("fix-readonly-evidence-capture", "Skipped", "AllowExternalConnections=false. No FIX/network call was made.", startedAt, DateTimeOffset.UtcNow, null, null, null, string.Empty, safetyDecisions, diagnostics);
        }

        if (options.AllowOrderSubmission)
        {
            return new LmaxFixReadOnlyEvidenceCaptureResult("fix-readonly-evidence-capture", "Failed", "AllowOrderSubmission must be false for read-only evidence capture.", startedAt, DateTimeOffset.UtcNow, null, null, null, string.Empty, safetyDecisions, diagnostics);
        }

        options.ShowFixMessages = showFixMessages;
        options.MarketDataMaxMessages = Math.Max(1, options.MarketDataMaxMessages);
        options.MarketDataMaxWaitSeconds = Math.Max(1, maxWaitSeconds);
        var marketData = await fixClient.MarketDataSnapshotSmokeAsync(options, cancellationToken);
        var tradeCaptureOptions = LmaxFixTradeCaptureRequestOptions.From(
            DateTimeOffset.UtcNow,
            Math.Max(1, tradeCaptureLookbackMinutes),
            null,
            null,
            account,
            Math.Max(1, maxWaitSeconds),
            Math.Max(1, maxReports),
            showFixMessages);
        var tradeCapture = await fixClient.TradeCaptureSmokeAsync(options, tradeCaptureOptions, cancellationToken);
        LmaxFixOrderStatusSmokeResult? orderStatus = null;
        if (!string.IsNullOrWhiteSpace(clOrdId))
        {
            orderStatus = await fixClient.OrderStatusSmokeAsync(
                options,
                new LmaxFixOrderStatusSmokeRequest(clOrdId, account, options.LmaxInstrumentId, options.FixSecurityIdSource, null, null, Math.Max(1, maxWaitSeconds), showFixMessages),
                cancellationToken);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var path = WriteReadOnlyEvidenceJson(outputDirectory, options, startedAt, completedAt, marketData, tradeCapture, orderStatus, diagnostics);
        var failed = marketData.Status == "Failed" || tradeCapture.Status == "Failed" || orderStatus?.Status == "Failed";
        var skipped = marketData.Status == "Skipped" && tradeCapture.Status == "Skipped" && orderStatus is null;
        return new LmaxFixReadOnlyEvidenceCaptureResult(
            "fix-readonly-evidence-capture",
            failed ? "Failed" : skipped ? "Skipped" : "Ok",
            $"Read-only evidence capture completed. Evidence JSON: {path}",
            startedAt,
            completedAt,
            marketData,
            tradeCapture,
            orderStatus,
            path,
            safetyDecisions,
            diagnostics);
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

    private static void WriteLifecycleEvidenceResult(LmaxFixLifecycleEvidenceResult result)
    {
        var report = result.EvidenceReport;
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        Console.WriteLine($"Message: {result.Message}");
        Console.WriteLine($"ClientOrderId: {report.ClientOrderId ?? "(missing)"}");
        Console.WriteLine($"BrokerOrderId: {report.BrokerOrderId ?? "(missing)"}");
        Console.WriteLine($"InstrumentSymbol: {report.InstrumentSymbol}");
        Console.WriteLine($"SecurityId: {report.SecurityId}");
        Console.WriteLine($"Side: {report.Side}");
        Console.WriteLine($"RequestedQuantity: {report.RequestedQuantity}");
        Console.WriteLine($"RequestedOrderType: {report.RequestedOrderType}");
        Console.WriteLine($"RequestedTimeInForce: {report.RequestedTimeInForce}");
        Console.WriteLine($"OrderSent: {report.OrderSent}");
        Console.WriteLine($"ExecutionReportCount: {report.ExecutionReportCount}");
        Console.WriteLine($"FillExecutionReportCount: {report.FillExecutionReportCount}");
        Console.WriteLine($"FinalOrdStatus: {report.FinalOrdStatus ?? "(missing)"}");
        Console.WriteLine($"FinalExecType: {report.FinalExecType ?? "(missing)"}");
        Console.WriteLine($"CumQty: {report.CumQty?.ToString() ?? "(missing)"}");
        Console.WriteLine($"LeavesQty: {report.LeavesQty?.ToString() ?? "(missing)"}");
        Console.WriteLine($"AvgPx: {report.AvgPx?.ToString() ?? "(missing)"}");
        Console.WriteLine($"LastFillExecId: {report.LastFillExecId ?? "(missing)"}");
        Console.WriteLine($"LastFillQty: {report.LastFillQty?.ToString() ?? "(missing)"}");
        Console.WriteLine($"LastFillPx: {report.LastFillPx?.ToString() ?? "(missing)"}");
        Console.WriteLine($"OrderStatusReceived: {report.OrderStatusReceived}");
        Console.WriteLine($"OrderStatusOrdStatus: {report.OrderStatusOrdStatus ?? "(missing)"}");
        Console.WriteLine($"OrderStatusCumQty: {report.OrderStatusCumQty?.ToString() ?? "(missing)"}");
        Console.WriteLine($"OrderStatusLeavesQty: {report.OrderStatusLeavesQty?.ToString() ?? "(missing)"}");
        Console.WriteLine($"TradeCaptureReceived: {report.TradeCaptureReceived}");
        Console.WriteLine($"TradeCaptureReportCount: {report.TradeCaptureReportCount}");
        Console.WriteLine($"TradeCaptureExecIds: {string.Join(",", report.TradeCaptureExecIds)}");
        foreach (var check in report.ConsistencyChecks)
        {
            Console.WriteLine($"ConsistencyCheck: Name=\"{check.Name}\" Status={check.Status} Expected={check.Expected ?? "(missing)"} Actual={check.Actual ?? "(missing)"} Message={check.Message}");
        }

        foreach (var warning in report.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
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

    private static void WriteReadOnlyEvidenceCaptureResult(LmaxFixReadOnlyEvidenceCaptureResult result)
    {
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"StartedAtUtc: {result.StartedAtUtc:O}");
        Console.WriteLine($"CompletedAtUtc: {result.CompletedAtUtc:O}");
        if (!string.IsNullOrWhiteSpace(result.EvidenceJsonPath)) Console.WriteLine($"EvidenceJsonPath: {result.EvidenceJsonPath}");
        if (!string.IsNullOrWhiteSpace(result.EvidenceJsonPath) && File.Exists(result.EvidenceJsonPath))
        {
            var validation = LmaxEvidenceContractValidator.ValidateAndNormalize(File.ReadAllText(result.EvidenceJsonPath));
            Console.WriteLine($"EvidenceValidation: {(validation.IsValid ? "Ok" : "Failed")} Mode={validation.EvidenceMode} Errors={validation.ErrorCount} Warnings={validation.WarningCount} Info={validation.Issues.Count(x => x.Severity == LmaxEvidenceContractIssueSeverity.Info)}");
            Console.WriteLine($"NoSensitiveContent: {!LmaxEvidenceContractValidator.ContainsSensitiveEvidence(validation.NormalizedJson)}");
            foreach (var issue in validation.Issues)
            {
                Console.WriteLine($"EvidenceValidationIssue: {issue.Severity} {issue.Path} {issue.Code} {issue.Message}");
            }
        }
        Console.WriteLine($"MarketDataStatus: {result.MarketData?.Status ?? "(not run)"}");
        Console.WriteLine($"MarketDataSnapshotReceived: {result.MarketData?.MarketDataSnapshotReceived ?? false}");
        Console.WriteLine($"ExecutionReportCount: 0");
        Console.WriteLine($"TradeCaptureStatus: {result.TradeCapture?.Status ?? "(not run)"}");
        Console.WriteLine($"TradeCaptureReportCount: {result.TradeCapture?.TradeReportCount ?? 0}");
        Console.WriteLine($"OrderStatusStatus: {result.OrderStatus?.Status ?? "(not requested)"}");
        Console.WriteLine($"OrderStatusExecutionReportCount: {result.OrderStatus?.ExecutionReports.Count ?? 0}");
        Console.WriteLine($"ProtocolRejectCount: {BuildReadOnlyProtocolRejects(result.TradeCapture, result.OrderStatus).Count()}");
        Console.WriteLine($"Message: {result.Message}");
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }

        foreach (var decision in result.SafetyDecisions)
        {
            Console.WriteLine($"- {decision}");
        }
    }

    private static string WriteReadOnlyEvidenceJson(
        string outputDirectory,
        LmaxConnectivityLabOptions options,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        LmaxFixMarketDataSmokeResult? marketData,
        LmaxFixTradeCaptureSmokeResult? tradeCapture,
        LmaxFixOrderStatusSmokeResult? orderStatus,
        IReadOnlyList<string> diagnostics)
    {
        var directory = Path.GetFullPath(outputDirectory);
        if (directory.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to write read-only evidence JSON to a UNC path.");
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"lmax-readonly-evidence-{completedAtUtc:yyyyMMdd-HHmmss}.json");
        var exported = new
        {
            schemaVersion = "lmax-fix-lifecycle-evidence-v1",
            createdAtUtc = completedAtUtc,
            capturedAtUtc = completedAtUtc,
            source = "ConnectivityLab",
            inputSource = "LabEvidenceFile",
            reason = "Replay LMAX read-only lab evidence",
            environment = options.EnvironmentName,
            captureMode = "ReadOnly",
            redaction = "SanitizedNoCredentialsNoRawLogon",
            dryRun = false,
            instrument = options.InstrumentSymbol,
            instrumentSymbol = options.InstrumentSymbol,
            lmaxInstrumentId = options.LmaxInstrumentId,
            securityId = options.LmaxInstrumentId,
            slashSymbol = options.LmaxSlashSymbol,
            startedAtUtc,
            completedAtUtc,
            marketData = marketData is null ? null : new
            {
                status = marketData.Status,
                snapshotReceived = marketData.MarketDataSnapshotReceived,
                bestBid = marketData.BestBid,
                bestAsk = marketData.BestAsk,
                mid = marketData.Mid,
                entryCount = marketData.Entries.Count,
                entries = marketData.Entries.Select(x => new
                {
                    symbol = x.Symbol,
                    securityId = x.SecurityId,
                    entryType = x.EntryType,
                    price = x.Price,
                    size = x.Size
                }).ToArray()
            },
            executionReports = Array.Empty<object>(),
            orderStatuses = orderStatus?.ExecutionReports.Select(ToShadowOrderStatusReport).ToArray() ?? [],
            tradeCaptureReports = tradeCapture?.Reports.Select(ToShadowTradeCaptureReport).ToArray() ?? [],
            protocolRejects = BuildReadOnlyProtocolRejects(tradeCapture, orderStatus).ToArray(),
            consistencyChecks = Array.Empty<object>(),
            warnings = BuildReadOnlyEvidenceWarnings(marketData, tradeCapture, orderStatus).Concat(diagnostics).ToArray()
        };

        var json = JsonSerializer.Serialize(exported, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        var validation = LmaxEvidenceContractValidator.ValidateAndNormalize(json);
        if (validation.ErrorCount > 0)
        {
            throw new InvalidOperationException("Refusing to write read-only evidence JSON because contract validation failed: " + string.Join("; ", validation.Issues.Where(x => x.Severity == LmaxEvidenceContractIssueSeverity.Error).Select(x => $"{x.Path}:{x.Code}")));
        }

        if (ContainsSensitiveEvidence(validation.NormalizedJson))
        {
            throw new InvalidOperationException("Refusing to write read-only evidence JSON because sanitized output contains sensitive markers.");
        }

        File.WriteAllText(path, validation.NormalizedJson);
        return path;
    }

    private static IEnumerable<object> BuildReadOnlyProtocolRejects(LmaxFixTradeCaptureSmokeResult? tradeCapture, LmaxFixOrderStatusSmokeResult? orderStatus)
    {
        if (tradeCapture?.RequestRejected == true)
        {
            yield return new
            {
                refMsgType = tradeCapture.RejectRefMsgType,
                refTagId = TryParseIntOrNull(tradeCapture.RejectRefTagId),
                reasonCode = TryParseIntOrNull(tradeCapture.RejectReasonCode),
                text = tradeCapture.RejectText ?? tradeCapture.AckRejectText,
                clientOrderId = (string?)null,
                brokerOrderId = (string?)null,
                payload = new { source = "TradeCaptureReportRequest" }
            };
        }

        if (orderStatus?.RequestRejected == true)
        {
            yield return new
            {
                refMsgType = orderStatus.RejectRefMsgType,
                refTagId = TryParseIntOrNull(orderStatus.RejectRefTagId),
                reasonCode = (int?)null,
                text = orderStatus.RejectText,
                clientOrderId = orderStatus.ClOrdId,
                brokerOrderId = orderStatus.BrokerOrderId,
                payload = new { source = "OrderStatusRequest" }
            };
        }
    }

    private static IEnumerable<string> BuildReadOnlyEvidenceWarnings(LmaxFixMarketDataSmokeResult? marketData, LmaxFixTradeCaptureSmokeResult? tradeCapture, LmaxFixOrderStatusSmokeResult? orderStatus)
    {
        if (marketData?.Status is "Skipped" or "Failed") yield return $"MarketData: {marketData.Message}";
        if (tradeCapture?.Status is "Skipped" or "Failed") yield return $"TradeCapture: {tradeCapture.Message}";
        if (orderStatus?.Status is "Skipped" or "Failed") yield return $"OrderStatus: {orderStatus.Message}";
        if (tradeCapture?.Reports.Count > 0) yield return "FIX AE does not currently provide TradeUTI; EOD remains official reconciliation source.";
    }

    private static void WriteLifecycleEvidenceJson(string outputJsonPath, LmaxFixLifecycleEvidenceResult result)
    {
        var fullPath = Path.GetFullPath(outputJsonPath);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to write lifecycle evidence JSON to a UNC path.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var report = result.EvidenceReport;
        var exported = new
        {
            schemaVersion = "lmax-fix-lifecycle-evidence-v1",
            createdAtUtc = DateTimeOffset.UtcNow,
            capturedAtUtc = DateTimeOffset.UtcNow,
            source = "ConnectivityLab",
            inputSource = "LabEvidenceFile",
            reason = "Replay LMAX lab lifecycle evidence",
            environment = "Demo",
            captureMode = "DemoLifecycleEvidence",
            redaction = "SanitizedNoCredentialsNoRawLogon",
            dryRun = !report.OrderSent,
            clientOrderId = report.ClientOrderId,
            brokerOrderId = report.BrokerOrderId,
            instrumentSymbol = report.InstrumentSymbol,
            securityId = report.SecurityId,
            side = report.Side,
            requestedQuantity = report.RequestedQuantity,
            requestedOrderType = report.RequestedOrderType,
            requestedTimeInForce = report.RequestedTimeInForce,
            executionReports = report.OrderSubmission.ExecutionReports.Select(ToShadowExecutionReport).ToArray(),
            orderStatuses = report.OrderStatusRecovery?.ExecutionReports.Select(ToShadowOrderStatusReport).ToArray() ?? [],
            tradeCaptureReports = report.TradeCaptureRecovery?.Reports.Select(ToShadowTradeCaptureReport).ToArray() ?? [],
            protocolRejects = result.OrderSubmission.RequestRejected
                ? new object[]
                {
                    new
                    {
                        refMsgType = result.OrderSubmission.RejectRefMsgType,
                        refTagId = TryParseIntOrNull(result.OrderSubmission.RejectRefTagId),
                        reasonCode = TryParseIntOrNull(result.OrderSubmission.RejectReasonCode),
                        text = result.OrderSubmission.RejectText,
                        clientOrderId = report.ClientOrderId,
                        brokerOrderId = report.BrokerOrderId
                    }
                }
                : Array.Empty<object>(),
            consistencyChecks = report.ConsistencyChecks.Select(x => new
            {
                name = x.Name,
                status = x.Status.ToString(),
                expected = x.Expected,
                actual = x.Actual,
                message = x.Message
            }).ToArray(),
            warnings = report.Warnings
        };

        var json = JsonSerializer.Serialize(exported, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        var validation = LmaxEvidenceContractValidator.ValidateAndNormalize(json);
        if (validation.ErrorCount > 0)
        {
            throw new InvalidOperationException("Refusing to write lifecycle evidence JSON because contract validation failed: " + string.Join("; ", validation.Issues.Where(x => x.Severity == LmaxEvidenceContractIssueSeverity.Error).Select(x => $"{x.Path}:{x.Code}")));
        }

        if (ContainsSensitiveEvidence(validation.NormalizedJson))
        {
            throw new InvalidOperationException("Refusing to write lifecycle evidence JSON because sanitized output contains sensitive markers.");
        }

        File.WriteAllText(fullPath, validation.NormalizedJson);
        Console.WriteLine($"LifecycleEvidenceJsonPath: {fullPath}");
    }

    private static int ValidateEvidenceFile(string? path, bool writeNormalizedCopy)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Evidence file path is required. Use --evidence-file=<path>.");
            return 2;
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"Evidence file not found: {fullPath}");
            return 2;
        }

        var validation = LmaxEvidenceContractValidator.ValidateAndNormalize(File.ReadAllText(fullPath));
        Console.WriteLine($"EvidenceFile: {fullPath}");
        Console.WriteLine($"SchemaVersion: {validation.SchemaVersion}");
        Console.WriteLine($"EvidenceMode: {validation.EvidenceMode}");
        Console.WriteLine($"ValidationStatus: {(validation.IsValid ? "Ok" : "Failed")}");
        Console.WriteLine($"Errors: {validation.ErrorCount}");
        Console.WriteLine($"Warnings: {validation.WarningCount}");
        Console.WriteLine($"NoSensitiveContent: {!LmaxEvidenceContractValidator.ContainsSensitiveEvidence(validation.NormalizedJson)}");
        foreach (var issue in validation.Issues)
        {
            Console.WriteLine($"{issue.Severity}: {issue.Path} {issue.Code} - {issue.Message}");
        }

        if (writeNormalizedCopy && validation.NormalizedJson.Length > 0)
        {
            var normalizedPath = Path.Combine(Path.GetDirectoryName(fullPath) ?? ".", Path.GetFileNameWithoutExtension(fullPath) + ".normalized.json");
            File.WriteAllText(normalizedPath, validation.NormalizedJson);
            Console.WriteLine($"NormalizedEvidencePath: {normalizedPath}");
        }

        return validation.IsValid ? 0 : 1;
    }

    private static object ToShadowExecutionReport(LmaxFixExecutionReport report) => new
    {
        execId = report.ExecId,
        brokerOrderId = report.OrderId,
        clientOrderId = report.ClOrdId,
        executionType = report.ExecType.ToString(),
        orderStatus = report.OrdStatus.ToString(),
        symbol = report.InternalSymbol ?? report.Symbol,
        side = report.Side?.ToString(),
        lastQty = report.LastQty,
        lastPx = report.LastPx,
        leavesQty = report.LeavesQty,
        cumQty = report.CumQty,
        avgPx = report.AvgPx,
        transactTimeUtc = report.TransactTimeUtc,
        payload = new
        {
            securityId = report.SecurityId,
            securityIdSource = report.SecurityIdSource,
            text = report.Text
        }
    };

    private static object ToShadowOrderStatusReport(LmaxFixExecutionReport report) => new
    {
        brokerOrderId = report.OrderId,
        clientOrderId = report.ClOrdId,
        symbol = report.InternalSymbol ?? report.Symbol,
        orderStatus = report.OrdStatus.ToString(),
        cumQty = report.CumQty,
        leavesQty = report.LeavesQty,
        transactTimeUtc = report.TransactTimeUtc,
        payload = new
        {
            execId = report.ExecId,
            executionType = report.ExecType.ToString(),
            securityId = report.SecurityId,
            securityIdSource = report.SecurityIdSource,
            text = report.Text
        }
    };

    private static object ToShadowTradeCaptureReport(LmaxFixTradeCaptureReport report) => new
    {
        execId = report.ExecId,
        secondaryExecId = report.SecondaryExecId,
        brokerOrderId = report.OrderId,
        clientOrderId = report.ClOrdId,
        symbol = report.InternalSymbol ?? report.Symbol,
        side = report.NormalizedSide?.ToString() ?? report.Side,
        lastQty = report.LastQty,
        lastPx = report.LastPx,
        tradeDate = NormalizeTradeDateForEvidence(report.TradeDate),
        transactTimeUtc = report.TransactTime,
        tradeUti = (string?)null,
        lastReportRequested = report.LastReportRequested,
        payload = new
        {
            securityId = report.SecurityId,
            securityIdSource = report.SecurityIdSource,
            tradeReportId = report.TradeReportId
        }
    };

    private static int? TryParseIntOrNull(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static string? NormalizeTradeDateForEvidence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compact))
        {
            return compact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static bool ContainsSensitiveEvidence(string json)
    {
        var lower = json.ToLowerInvariant();
        return lower.Contains("554=", StringComparison.Ordinal)
            || lower.Contains("password", StringComparison.Ordinal)
            || lower.Contains("authorization", StringComparison.Ordinal)
            || lower.Contains("bearer ", StringComparison.Ordinal)
            || lower.Contains("x-api-key", StringComparison.Ordinal)
            || lower.Contains("secret", StringComparison.Ordinal)
            || lower.Contains("token", StringComparison.Ordinal);
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
