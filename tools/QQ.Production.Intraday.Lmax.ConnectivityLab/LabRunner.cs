namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class LmaxConnectivityLabRunner(
    ILmaxPublicDataClient publicDataClient,
    ILmaxAccountClient accountClient,
    ILmaxFixSessionClient fixClient,
    LmaxConnectivityLabSafetyValidator safety)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var command = args.FirstOrDefault(x => !x.StartsWith('-')) ?? "print-config";
        var optionArgs = args.Where(x => x.StartsWith('-')).ToArray();
        var options = LmaxConnectivityLabOptions.FromEnvironmentAndArgs(optionArgs);
        var explicitConfirm = args.Any(x => x.Equals("--confirm-demo-order", StringComparison.OrdinalIgnoreCase));

        var result = command.ToLowerInvariant() switch
        {
            "print-config" => PrintConfig(options),
            "check-public-data-config" => CheckPublicDataConfig(options),
            "public-data-smoke" => await publicDataClient.SmokeAsync(options, cancellationToken),
            "account-api-smoke" => await accountClient.SmokeAsync(options, cancellationToken),
            "fix-session-dry-run" => fixClient.Validate(options, marketData: false),
            "fix-market-data-smoke" => await fixClient.SmokeAsync(options, marketData: true, cancellationToken),
            "fix-order-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: false, cancellationToken),
            "fix-marketdata-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: true, cancellationToken),
            "fix-market-data-logon-smoke" => await fixClient.LogonSmokeAsync(options, marketData: true, cancellationToken),
            "fix-marketdata-snapshot-smoke" => fixClient.SnapshotSmoke(options),
            "fix-market-data-snapshot-smoke" => fixClient.SnapshotSmoke(options),
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
}
