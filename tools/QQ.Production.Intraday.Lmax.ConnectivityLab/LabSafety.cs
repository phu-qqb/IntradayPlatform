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

    public IReadOnlyList<LmaxFixDemoOrderSafetyDecision> ValidateForDemoOrderLifecycle(LmaxConnectivityLabOptions options, LmaxFixDemoOrderRequest request, bool explicitConfirmation)
    {
        var decisions = new List<LmaxFixDemoOrderSafetyDecision>
        {
            Decision("Environment", IsDemoOrUat(options.EnvironmentName), "EnvironmentName must be Demo or UAT."),
            Decision("AllowExternalConnections", options.AllowExternalConnections, "AllowExternalConnections must be true."),
            Decision("AllowOrderSubmission", options.AllowOrderSubmission, "AllowOrderSubmission must be true."),
            Decision("AllowLiveTrading", !options.AllowLiveTrading, "AllowLiveTrading must remain false."),
            Decision("DryRun", !request.DryRun && !options.DryRun, "DryRun must be false for live demo order send."),
            Decision("ConfirmDemoOrder", request.ConfirmDemoOrder, "ConfirmDemoOrder must be true."),
            Decision("ExplicitCommandFlag", explicitConfirmation, "Command must include --confirm-demo-order."),
            Decision("DemoHost", IsKnownLmaxDemoOrUatHost(options.FixOrderHost ?? string.Empty), "FIX order host must be an LMAX demo/UAT host."),
            Decision("QuantityLimit", request.VenueQuantity > 0m && request.VenueQuantity <= options.MaxDemoOrderQuantity, $"VenueQuantity must be > 0 and <= {options.MaxDemoOrderQuantity}.")
        };

        if (request.OrderType == LmaxFixDemoOrderType.Limit && request.LimitPrice is null)
        {
            decisions.Add(Decision("LimitPrice", false, "LimitPrice is required for Limit demo orders."));
        }
        else if (request.LimitPrice.HasValue && request.MaxNotionalUsd.HasValue)
        {
            var notional = request.VenueQuantity * request.LimitPrice.Value;
            decisions.Add(Decision("NotionalLimit", notional <= request.MaxNotionalUsd.Value, $"Estimated notional {notional} must be <= {request.MaxNotionalUsd.Value}."));
        }
        else
        {
            decisions.Add(Decision("NotionalLimit", true, "No limit price was available; max notional check is informational for market orders."));
        }

        if (string.IsNullOrWhiteSpace(options.FixOrderHost)) decisions.Add(Decision("FixOrderHost", false, "Missing FixOrderHost."));
        if (options.FixOrderPort is null) decisions.Add(Decision("FixOrderPort", false, "Missing FixOrderPort."));
        if (string.IsNullOrWhiteSpace(options.FixOrderTargetCompId ?? options.FixTargetCompId)) decisions.Add(Decision("FixOrderTargetCompId", false, "Missing FixOrderTargetCompId."));
        if (string.IsNullOrWhiteSpace(options.FixSenderCompId)) decisions.Add(Decision("FixSenderCompId", false, "Missing FixSenderCompId."));
        if (string.IsNullOrWhiteSpace(options.FixUsername)) decisions.Add(Decision("FixUsername", false, "Missing FixUsername."));
        if (string.IsNullOrWhiteSpace(options.FixPassword)) decisions.Add(Decision("FixPassword", false, "Missing FixPassword."));
        return decisions;
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

    public IReadOnlyList<string> ValidateForAccountApi(LmaxConnectivityLabOptions options)
    {
        var issues = new List<string>();
        if (options.AllowLiveTrading) issues.Add("Account API smoke is blocked because AllowLiveTrading=true.");
        if (options.AllowOrderSubmission) issues.Add("Account API smoke requires AllowOrderSubmission=false.");
        if (!options.AllowExternalConnections) issues.Add("Account API smoke is skipped because AllowExternalConnections=false.");
        if (!IsDemoOrUat(options.EnvironmentName)) issues.Add("Account API smoke is allowed only in Demo or UAT environments.");
        if (string.IsNullOrWhiteSpace(options.AccountApiBaseUrl)) issues.Add("Missing AccountApiBaseUrl.");
        else if (!Uri.TryCreate(options.AccountApiBaseUrl, UriKind.Absolute, out var uri)) issues.Add("AccountApiBaseUrl is not a valid absolute URI.");
        else
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) issues.Add("AccountApiBaseUrl must use HTTPS.");
            if (!IsKnownLmaxDemoOrUatHost(uri.Host)) issues.Add("AccountApiBaseUrl must be an LMAX demo/UAT host for this lab command.");
        }

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

    private static bool IsKnownLmaxDemoOrUatHost(string host)
        => host.EndsWith(".lmax.com", StringComparison.OrdinalIgnoreCase)
           && (host.Contains("demo", StringComparison.OrdinalIgnoreCase) || host.Contains("uat", StringComparison.OrdinalIgnoreCase));

    private static LmaxFixDemoOrderSafetyDecision Decision(string gate, bool passed, string message)
        => new(gate, passed, message);
}
