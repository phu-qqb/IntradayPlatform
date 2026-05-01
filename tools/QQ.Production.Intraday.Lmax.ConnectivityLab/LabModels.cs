namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class LmaxConnectivityLabOptions
{
    public bool Enabled { get; set; } = false;
    public string EnvironmentName { get; set; } = "Local";
    public bool AllowExternalConnections { get; set; } = false;
    public bool AllowOrderSubmission { get; set; } = false;
    public bool AllowLiveTrading { get; set; } = false;
    public bool DryRun { get; set; } = true;
    public string VenueName { get; set; } = "LMAX";
    public string AccountCode { get; set; } = "LMAX_DEMO_LOCAL";
    public string? AccountApiBaseUrl { get; set; }
    public string? PublicDataApiBaseUrl { get; set; }
    public string? FixOrderHost { get; set; }
    public int? FixOrderPort { get; set; }
    public string? FixMarketDataHost { get; set; }
    public int? FixMarketDataPort { get; set; }
    public string? FixSenderCompId { get; set; }
    public string? FixTargetCompId { get; set; }
    public string? FixUsername { get; set; }
    public bool UseTls { get; set; } = true;
    public string InstrumentSymbol { get; set; } = "EURUSD";
    public string LmaxInstrumentId { get; set; } = "4001";
    public int RequestTimeoutSeconds { get; set; } = 10;
    public string? AccountApiKey { get; set; }

    public static LmaxConnectivityLabOptions FromEnvironmentAndArgs(string[] args)
    {
        var options = new LmaxConnectivityLabOptions
        {
            Enabled = ReadBool("QQ_LMAX_LAB_ENABLED", false),
            EnvironmentName = Environment.GetEnvironmentVariable("QQ_LMAX_ENVIRONMENT") ?? "Local",
            AllowExternalConnections = ReadBool("QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS", false),
            AllowOrderSubmission = ReadBool("QQ_LMAX_ALLOW_ORDER_SUBMISSION", false),
            AllowLiveTrading = ReadBool("QQ_LMAX_ALLOW_LIVE_TRADING", false),
            DryRun = ReadBool("QQ_LMAX_DRY_RUN", true),
            VenueName = Environment.GetEnvironmentVariable("QQ_LMAX_VENUE_NAME") ?? "LMAX",
            AccountCode = Environment.GetEnvironmentVariable("QQ_LMAX_ACCOUNT_CODE") ?? "LMAX_DEMO_LOCAL",
            AccountApiBaseUrl = Environment.GetEnvironmentVariable("QQ_LMAX_ACCOUNT_API_BASE_URL"),
            PublicDataApiBaseUrl = Environment.GetEnvironmentVariable("QQ_LMAX_PUBLIC_DATA_API_BASE_URL"),
            FixOrderHost = Environment.GetEnvironmentVariable("QQ_LMAX_FIX_ORDER_HOST"),
            FixOrderPort = ReadInt("QQ_LMAX_FIX_ORDER_PORT"),
            FixMarketDataHost = Environment.GetEnvironmentVariable("QQ_LMAX_FIX_MARKET_DATA_HOST"),
            FixMarketDataPort = ReadInt("QQ_LMAX_FIX_MARKET_DATA_PORT"),
            FixSenderCompId = Environment.GetEnvironmentVariable("QQ_LMAX_FIX_SENDER_COMP_ID"),
            FixTargetCompId = Environment.GetEnvironmentVariable("QQ_LMAX_FIX_TARGET_COMP_ID"),
            FixUsername = Environment.GetEnvironmentVariable("QQ_LMAX_FIX_USERNAME"),
            UseTls = ReadBool("QQ_LMAX_USE_TLS", true),
            InstrumentSymbol = Environment.GetEnvironmentVariable("QQ_LMAX_INSTRUMENT_SYMBOL") ?? "EURUSD",
            LmaxInstrumentId = Environment.GetEnvironmentVariable("QQ_LMAX_INSTRUMENT_ID") ?? "4001",
            RequestTimeoutSeconds = ReadInt("QQ_LMAX_REQUEST_TIMEOUT_SECONDS") ?? 10,
            AccountApiKey = Environment.GetEnvironmentVariable("QQ_LMAX_ACCOUNT_API_KEY")
        };

        foreach (var arg in args)
        {
            var parts = arg.TrimStart('-').Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].ToLowerInvariant();
            var value = parts[1];
            if (key == "environment") options.EnvironmentName = value;
            if (key == "allow-external-connections") options.AllowExternalConnections = bool.Parse(value);
            if (key == "allow-order-submission") options.AllowOrderSubmission = bool.Parse(value);
            if (key == "allow-live-trading") options.AllowLiveTrading = bool.Parse(value);
            if (key == "dry-run") options.DryRun = bool.Parse(value);
            if (key == "public-data-api-base-url") options.PublicDataApiBaseUrl = value;
            if (key == "account-api-base-url") options.AccountApiBaseUrl = value;
            if (key == "fix-order-host") options.FixOrderHost = value;
            if (key == "fix-order-port") options.FixOrderPort = int.Parse(value);
            if (key == "fix-market-data-host") options.FixMarketDataHost = value;
            if (key == "fix-market-data-port") options.FixMarketDataPort = int.Parse(value);
            if (key == "fix-sender-comp-id") options.FixSenderCompId = value;
            if (key == "fix-target-comp-id") options.FixTargetCompId = value;
            if (key == "fix-username") options.FixUsername = value;
            if (key == "instrument-symbol") options.InstrumentSymbol = value;
            if (key == "lmax-instrument-id") options.LmaxInstrumentId = value;
        }

        return options;
    }

    public IReadOnlyDictionary<string, string> ToSafeDictionary()
        => new Dictionary<string, string>
        {
            ["Enabled"] = Enabled.ToString(),
            ["EnvironmentName"] = EnvironmentName,
            ["AllowExternalConnections"] = AllowExternalConnections.ToString(),
            ["AllowOrderSubmission"] = AllowOrderSubmission.ToString(),
            ["AllowLiveTrading"] = AllowLiveTrading.ToString(),
            ["DryRun"] = DryRun.ToString(),
            ["VenueName"] = VenueName,
            ["AccountCode"] = AccountCode,
            ["AccountApiBaseUrl"] = AccountApiBaseUrl ?? "(not configured)",
            ["PublicDataApiBaseUrl"] = PublicDataApiBaseUrl ?? "(not configured)",
            ["FixOrderHost"] = FixOrderHost ?? "(not configured)",
            ["FixOrderPort"] = FixOrderPort?.ToString() ?? "(not configured)",
            ["FixMarketDataHost"] = FixMarketDataHost ?? "(not configured)",
            ["FixMarketDataPort"] = FixMarketDataPort?.ToString() ?? "(not configured)",
            ["FixSenderCompId"] = Mask(FixSenderCompId),
            ["FixTargetCompId"] = FixTargetCompId ?? "(not configured)",
            ["FixUsername"] = Mask(FixUsername),
            ["UseTls"] = UseTls.ToString(),
            ["InstrumentSymbol"] = InstrumentSymbol,
            ["LmaxInstrumentId"] = LmaxInstrumentId,
            ["RequestTimeoutSeconds"] = RequestTimeoutSeconds.ToString(),
            ["AccountApiKey"] = Mask(AccountApiKey)
        };

    public static string Mask(string? value)
        => string.IsNullOrWhiteSpace(value) ? "(not configured)" : "********";

    private static bool ReadBool(string name, bool defaultValue)
        => bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;

    private static int? ReadInt(string name)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : null;
}

public sealed record LabCommandResult(string Command, string Status, string Message, IReadOnlyList<string> SafetyDecisions)
{
    public bool IsSuccess => Status is "Ok" or "Skipped";
    public static LabCommandResult Ok(string command, string message, IReadOnlyList<string> decisions) => new(command, "Ok", message, decisions);
    public static LabCommandResult Skipped(string command, string message, IReadOnlyList<string> decisions) => new(command, "Skipped", message, decisions);
    public static LabCommandResult Blocked(string command, string message, IReadOnlyList<string> decisions) => new(command, "Blocked", message, decisions);
}

public sealed record LmaxFixOptions(string? Host, int? Port, string? SenderCompId, string? TargetCompId, string? Username, bool UseTls);
public sealed record LmaxFixSessionHealth(bool Configured, bool Connected, string Message);
public sealed record LmaxFixMessageLogEntry(DateTimeOffset TimestampUtc, string Direction, string MessageType, string SafeSummary);

public interface ILmaxPublicDataClient
{
    Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken);
}

public interface ILmaxAccountClient
{
    Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken);
}

public interface ILmaxFixSessionClient
{
    LabCommandResult Validate(LmaxConnectivityLabOptions options, bool marketData);
    Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken);
}

public sealed class PlaceholderLmaxPublicDataClient : ILmaxPublicDataClient
{
    public Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken)
    {
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList();
        if (!options.AllowExternalConnections) return Task.FromResult(LabCommandResult.Skipped("public-data-smoke", "External connections are disabled.", decisions));
        if (string.IsNullOrWhiteSpace(options.PublicDataApiBaseUrl)) return Task.FromResult(LabCommandResult.Skipped("public-data-smoke", "Public data API base URL is not configured.", decisions));
        return Task.FromResult(LabCommandResult.Skipped("public-data-smoke", "No official LMAX public data client is wired into the lab yet.", decisions));
    }
}

public sealed class PlaceholderLmaxAccountClient : ILmaxAccountClient
{
    public Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken)
    {
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList();
        if (!options.AllowExternalConnections) return Task.FromResult(LabCommandResult.Skipped("account-api-smoke", "External connections are disabled.", decisions));
        if (string.IsNullOrWhiteSpace(options.AccountApiBaseUrl)) return Task.FromResult(LabCommandResult.Skipped("account-api-smoke", "Account API base URL is not configured.", decisions));
        if (string.IsNullOrWhiteSpace(options.AccountApiKey)) return Task.FromResult(LabCommandResult.Skipped("account-api-smoke", "Account API key is not configured.", decisions));
        return Task.FromResult(LabCommandResult.Skipped("account-api-smoke", "No official LMAX account API client is wired into the lab yet.", decisions));
    }
}

public sealed class PlaceholderLmaxFixSessionClient : ILmaxFixSessionClient
{
    public LabCommandResult Validate(LmaxConnectivityLabOptions options, bool marketData)
    {
        var missing = RequiredFixFields(options, marketData).ToList();
        var command = marketData ? "fix-market-data-smoke" : "fix-session-dry-run";
        if (missing.Count > 0) return LabCommandResult.Skipped(command, $"Missing FIX config: {string.Join(", ", missing)}.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList());
        return LabCommandResult.Ok(command, "FIX configuration is structurally complete. No socket connection was opened.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList());
    }

    public Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
    {
        var command = marketData ? "fix-market-data-smoke" : "fix-session-dry-run";
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList();
        if (!options.AllowExternalConnections) return Task.FromResult(LabCommandResult.Skipped(command, "External connections are disabled.", decisions));
        var validation = Validate(options, marketData);
        if (validation.Status != "Ok") return Task.FromResult(validation);
        return Task.FromResult(LabCommandResult.Skipped(command, "No QuickFIX/n LMAX session implementation is wired into the lab yet.", decisions));
    }

    private static IEnumerable<string> RequiredFixFields(LmaxConnectivityLabOptions options, bool marketData)
    {
        if (marketData)
        {
            if (string.IsNullOrWhiteSpace(options.FixMarketDataHost)) yield return nameof(options.FixMarketDataHost);
            if (options.FixMarketDataPort is null) yield return nameof(options.FixMarketDataPort);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.FixOrderHost)) yield return nameof(options.FixOrderHost);
            if (options.FixOrderPort is null) yield return nameof(options.FixOrderPort);
        }

        if (string.IsNullOrWhiteSpace(options.FixSenderCompId)) yield return nameof(options.FixSenderCompId);
        if (string.IsNullOrWhiteSpace(options.FixTargetCompId)) yield return nameof(options.FixTargetCompId);
        if (string.IsNullOrWhiteSpace(options.FixUsername)) yield return nameof(options.FixUsername);
    }
}
