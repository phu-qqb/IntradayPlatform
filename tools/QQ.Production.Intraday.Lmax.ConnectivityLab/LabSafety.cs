namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class LmaxConnectivityLabSafetyValidator
{
    public IReadOnlyList<string> ValidateForExternalCall(LmaxConnectivityLabOptions options)
    {
        var issues = new List<string>();
        if (options.AllowLiveTrading) issues.Add("AllowLiveTrading=true is forbidden in the connectivity lab.");
        if (!options.AllowExternalConnections) issues.Add("External calls are blocked because AllowExternalConnections=false.");
        return issues;
    }

    public IReadOnlyList<string> ValidateForOrderSubmission(LmaxConnectivityLabOptions options, bool explicitConfirmation)
    {
        var issues = ValidateForExternalCall(options).ToList();
        if (!options.AllowOrderSubmission) issues.Add("Order submission is blocked because AllowOrderSubmission=false.");
        if (options.DryRun) issues.Add("Order submission is blocked because DryRun=true.");
        if (!explicitConfirmation) issues.Add("Order submission requires explicit command-line confirmation.");
        if (!IsDemoOrUat(options.EnvironmentName)) issues.Add("Order submission is allowed only in Demo or UAT environments.");
        if (options.EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)) issues.Add("Order submission is blocked in Production.");
        return issues;
    }

    public IReadOnlyList<string> ValidateForFixLogon(LmaxConnectivityLabOptions options, bool marketData)
    {
        var issues = new List<string>();
        if (options.AllowLiveTrading) issues.Add("FIX logon smoke is blocked because AllowLiveTrading=true.");
        if (!options.AllowExternalConnections) issues.Add("FIX logon smoke is skipped because AllowExternalConnections=false.");
        if (!IsDemoOrUat(options.EnvironmentName)) issues.Add("FIX logon smoke is allowed only in Demo or UAT environments.");
        if (options.AllowOrderSubmission) issues.Add("FIX logon smoke requires AllowOrderSubmission=false.");

        if (marketData)
        {
            if (string.IsNullOrWhiteSpace(options.FixMarketDataHost)) issues.Add("Missing FixMarketDataHost.");
            if (options.FixMarketDataPort is null) issues.Add("Missing FixMarketDataPort.");
            if (string.IsNullOrWhiteSpace(options.FixMarketDataTargetCompId ?? options.FixTargetCompId)) issues.Add("Missing FixMarketDataTargetCompId.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.FixOrderHost)) issues.Add("Missing FixOrderHost.");
            if (options.FixOrderPort is null) issues.Add("Missing FixOrderPort.");
            if (string.IsNullOrWhiteSpace(options.FixOrderTargetCompId ?? options.FixTargetCompId)) issues.Add("Missing FixOrderTargetCompId.");
        }

        if (string.IsNullOrWhiteSpace(options.FixSenderCompId)) issues.Add("Missing FixSenderCompId.");
        if (string.IsNullOrWhiteSpace(options.FixUsername)) issues.Add("Missing FixUsername.");
        if (string.IsNullOrWhiteSpace(options.FixPassword)) issues.Add("Missing FixPassword.");
        return issues;
    }

    public static IReadOnlyList<string> DecisionsForExternalCommand(LmaxConnectivityLabOptions options)
    {
        var decisions = new List<string>
        {
            $"EnvironmentName={options.EnvironmentName}",
            $"AllowExternalConnections={options.AllowExternalConnections}",
            $"AllowOrderSubmission={options.AllowOrderSubmission}",
            $"AllowLiveTrading={options.AllowLiveTrading}",
            $"DryRun={options.DryRun}"
        };

        if (!options.AllowExternalConnections) decisions.Add("No external network call will be made.");
        if (options.AllowLiveTrading) decisions.Add("Blocked: live trading is forbidden.");
        return decisions;
    }

    public static bool IsDemoOrUat(string environmentName)
        => environmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase) || environmentName.Equals("UAT", StringComparison.OrdinalIgnoreCase);
}
