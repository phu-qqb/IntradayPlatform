namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Text.Json;

public sealed class LmaxConnectivityLabOptions
{
    private const string UserSecretsId = "1a8cb76b-1148-4a9e-978c-ab4ecd70e65e";

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
    public string? FixOrderTargetCompId { get; set; }
    public string? FixMarketDataTargetCompId { get; set; }
    public string? FixTargetCompId { get; set; }
    public string? FixUsername { get; set; }
    public string? FixPassword { get; set; }
    public bool UseTls { get; set; } = true;
    public string InstrumentSymbol { get; set; } = "EURUSD";
    public string LmaxInstrumentId { get; set; } = "4001";
    public string LmaxSlashSymbol { get; set; } = "EUR/USD";
    public string FixSecurityIdSource { get; set; } = "8";
    public int MarketDepth { get; set; } = 1;
    public LmaxFixMarketDataRequestMode MarketDataRequestMode { get; set; } = LmaxFixMarketDataRequestMode.SnapshotOnly;
    public int ConnectTimeoutSeconds { get; set; } = 10;
    public int LogonTimeoutSeconds { get; set; } = 10;
    public int MarketDataMaxWaitSeconds { get; set; } = 10;
    public int MarketDataMaxMessages { get; set; } = 5;
    public LmaxFixMarketDataSymbolEncodingMode MarketDataSymbolEncodingMode { get; set; } = LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbol;
    public bool ShowFixMessages { get; set; } = false;
    public int RequestTimeoutSeconds { get; set; } = 10;
    public string? AccountApiKey { get; set; }

    public static LmaxConnectivityLabOptions FromEnvironmentAndArgs(string[] args)
    {
        var values = LoadConfigurationValues();
        ApplyEnvironment(values);

        var options = new LmaxConnectivityLabOptions();
        ApplyValues(options, values);

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
            if (key == "fix-order-target-comp-id") options.FixOrderTargetCompId = value;
            if (key == "fix-market-data-target-comp-id") options.FixMarketDataTargetCompId = value;
            if (key == "fix-target-comp-id") options.FixTargetCompId = value;
            if (key == "fix-username") options.FixUsername = value;
            if (key == "fix-password") options.FixPassword = value;
            if (key == "instrument-symbol") options.InstrumentSymbol = value;
            if (key == "instrument") options.InstrumentSymbol = value;
            if (key == "lmax-instrument-id") options.LmaxInstrumentId = value;
            if (key == "slash-symbol") options.LmaxSlashSymbol = value;
            if (key == "fix-security-id-source") options.FixSecurityIdSource = value;
            if (key == "market-depth") options.MarketDepth = int.Parse(value);
            if (key == "request-mode") options.MarketDataRequestMode = Enum.Parse<LmaxFixMarketDataRequestMode>(value, ignoreCase: true);
            if (key == "symbol-encoding-mode") options.MarketDataSymbolEncodingMode = Enum.Parse<LmaxFixMarketDataSymbolEncodingMode>(value, ignoreCase: true);
            if (key == "show-fix-messages") options.ShowFixMessages = bool.Parse(value);
            if (key == "connect-timeout-seconds") options.ConnectTimeoutSeconds = int.Parse(value);
            if (key == "logon-timeout-seconds") options.LogonTimeoutSeconds = int.Parse(value);
            if (key == "max-wait-seconds") options.MarketDataMaxWaitSeconds = int.Parse(value);
            if (key == "max-messages") options.MarketDataMaxMessages = int.Parse(value);
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
            ["FixOrderTargetCompId"] = FixOrderTargetCompId ?? FixTargetCompId ?? "(not configured)",
            ["FixMarketDataTargetCompId"] = FixMarketDataTargetCompId ?? FixTargetCompId ?? "(not configured)",
            ["FixTargetCompId"] = FixTargetCompId ?? "(not configured)",
            ["FixUsername"] = Mask(FixUsername),
            ["FixPassword"] = Mask(FixPassword),
            ["UseTls"] = UseTls.ToString(),
            ["InstrumentSymbol"] = InstrumentSymbol,
            ["LmaxInstrumentId"] = LmaxInstrumentId,
            ["LmaxSlashSymbol"] = LmaxSlashSymbol,
            ["FixSecurityIdSource"] = FixSecurityIdSource,
            ["MarketDepth"] = MarketDepth.ToString(),
            ["MarketDataRequestMode"] = MarketDataRequestMode.ToString(),
            ["ConnectTimeoutSeconds"] = ConnectTimeoutSeconds.ToString(),
            ["LogonTimeoutSeconds"] = LogonTimeoutSeconds.ToString(),
            ["MarketDataMaxWaitSeconds"] = MarketDataMaxWaitSeconds.ToString(),
            ["MarketDataMaxMessages"] = MarketDataMaxMessages.ToString(),
            ["MarketDataSymbolEncodingMode"] = MarketDataSymbolEncodingMode.ToString(),
            ["ShowFixMessages"] = ShowFixMessages.ToString(),
            ["RequestTimeoutSeconds"] = RequestTimeoutSeconds.ToString(),
            ["AccountApiKey"] = Mask(AccountApiKey)
        };

    public static string Mask(string? value)
        => string.IsNullOrWhiteSpace(value) ? "(not configured)" : "********";

    private static bool ReadBool(string name, bool defaultValue)
        => bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;

    private static int? ReadInt(string name)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : null;

    private static Dictionary<string, string> LoadConfigurationValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadJson(values, Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        LoadJson(values, Path.Combine(Directory.GetCurrentDirectory(), "tools", "QQ.Production.Intraday.Lmax.ConnectivityLab", "appsettings.json"));

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            LoadJson(values, Path.Combine(appData, "Microsoft", "UserSecrets", UserSecretsId, "secrets.json"));
        }

        return values;
    }

    private static void LoadJson(IDictionary<string, string> values, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            FlattenJson(values, document.RootElement, null);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or JsonException)
        {
            // Configuration files are optional for the lab. Environment variables and command-line args still work.
        }
    }

    private static void FlattenJson(IDictionary<string, string> values, JsonElement element, string? prefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                FlattenJson(values, property.Value, prefix is null ? property.Name : $"{prefix}:{property.Name}");
            }

            return;
        }

        if (prefix is not null)
        {
            values[prefix] = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();
        }
    }

    private static void ApplyEnvironment(IDictionary<string, string> values)
    {
        SetIfPresent(values, "LmaxConnectivityLab:Enabled", "QQ_LMAX_LAB_ENABLED", "LmaxConnectivityLab__Enabled");
        SetIfPresent(values, "LmaxConnectivityLab:EnvironmentName", "QQ_LMAX_ENVIRONMENT", "LmaxConnectivityLab__EnvironmentName");
        SetIfPresent(values, "LmaxConnectivityLab:AllowExternalConnections", "QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS", "LmaxConnectivityLab__AllowExternalConnections");
        SetIfPresent(values, "LmaxConnectivityLab:AllowOrderSubmission", "QQ_LMAX_ALLOW_ORDER_SUBMISSION", "LmaxConnectivityLab__AllowOrderSubmission");
        SetIfPresent(values, "LmaxConnectivityLab:AllowLiveTrading", "QQ_LMAX_ALLOW_LIVE_TRADING", "LmaxConnectivityLab__AllowLiveTrading");
        SetIfPresent(values, "LmaxConnectivityLab:DryRun", "QQ_LMAX_DRY_RUN", "LmaxConnectivityLab__DryRun");
        SetIfPresent(values, "LmaxConnectivityLab:VenueName", "QQ_LMAX_VENUE_NAME", "LmaxConnectivityLab__VenueName");
        SetIfPresent(values, "LmaxConnectivityLab:AccountCode", "QQ_LMAX_ACCOUNT_CODE", "LmaxConnectivityLab__AccountCode");
        SetIfPresent(values, "LmaxConnectivityLab:AccountApiBaseUrl", "QQ_LMAX_ACCOUNT_API_BASE_URL", "LmaxConnectivityLab__AccountApiBaseUrl");
        SetIfPresent(values, "LmaxConnectivityLab:PublicDataApiBaseUrl", "QQ_LMAX_PUBLIC_DATA_API_BASE_URL", "LmaxConnectivityLab__PublicDataApiBaseUrl");
        SetIfPresent(values, "LmaxConnectivityLab:FixOrderHost", "QQ_LMAX_FIX_ORDER_HOST", "LmaxConnectivityLab__FixOrderHost");
        SetIfPresent(values, "LmaxConnectivityLab:FixOrderPort", "QQ_LMAX_FIX_ORDER_PORT", "LmaxConnectivityLab__FixOrderPort");
        SetIfPresent(values, "LmaxConnectivityLab:FixMarketDataHost", "QQ_LMAX_FIX_MARKET_DATA_HOST", "LmaxConnectivityLab__FixMarketDataHost");
        SetIfPresent(values, "LmaxConnectivityLab:FixMarketDataPort", "QQ_LMAX_FIX_MARKET_DATA_PORT", "LmaxConnectivityLab__FixMarketDataPort");
        SetIfPresent(values, "LmaxConnectivityLab:FixSenderCompId", "QQ_LMAX_FIX_SENDER_COMP_ID", "LmaxConnectivityLab__FixSenderCompId");
        SetIfPresent(values, "LmaxConnectivityLab:FixOrderTargetCompId", "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID", "LmaxConnectivityLab__FixOrderTargetCompId");
        SetIfPresent(values, "LmaxConnectivityLab:FixMarketDataTargetCompId", "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID", "LmaxConnectivityLab__FixMarketDataTargetCompId");
        SetIfPresent(values, "LmaxConnectivityLab:FixTargetCompId", "QQ_LMAX_FIX_TARGET_COMP_ID", "LmaxConnectivityLab__FixTargetCompId");
        SetIfPresent(values, "LmaxConnectivityLab:FixUsername", "QQ_LMAX_FIX_USERNAME", "LmaxConnectivityLab__FixUsername");
        SetIfPresent(values, "LmaxConnectivityLab:FixPassword", "QQ_LMAX_FIX_PASSWORD", "LmaxConnectivityLab__FixPassword");
        SetIfPresent(values, "LmaxConnectivityLab:UseTls", "QQ_LMAX_USE_TLS", "LmaxConnectivityLab__UseTls");
        SetIfPresent(values, "LmaxConnectivityLab:InstrumentSymbol", "QQ_LMAX_INSTRUMENT_SYMBOL", "LmaxConnectivityLab__InstrumentSymbol");
        SetIfPresent(values, "LmaxConnectivityLab:LmaxInstrumentId", "QQ_LMAX_INSTRUMENT_ID", "LmaxConnectivityLab__LmaxInstrumentId");
        SetIfPresent(values, "LmaxConnectivityLab:LmaxSlashSymbol", "QQ_LMAX_SLASH_SYMBOL", "LmaxConnectivityLab__LmaxSlashSymbol");
        SetIfPresent(values, "LmaxConnectivityLab:FixSecurityIdSource", "QQ_LMAX_FIX_SECURITY_ID_SOURCE", "LmaxConnectivityLab__FixSecurityIdSource");
        SetIfPresent(values, "LmaxConnectivityLab:MarketDepth", "QQ_LMAX_MARKET_DEPTH", "LmaxConnectivityLab__MarketDepth");
        SetIfPresent(values, "LmaxConnectivityLab:MarketDataRequestMode", "QQ_LMAX_MARKET_DATA_REQUEST_MODE", "LmaxConnectivityLab__MarketDataRequestMode");
        SetIfPresent(values, "LmaxConnectivityLab:ConnectTimeoutSeconds", "QQ_LMAX_CONNECT_TIMEOUT_SECONDS", "LmaxConnectivityLab__ConnectTimeoutSeconds");
        SetIfPresent(values, "LmaxConnectivityLab:LogonTimeoutSeconds", "QQ_LMAX_LOGON_TIMEOUT_SECONDS", "LmaxConnectivityLab__LogonTimeoutSeconds");
        SetIfPresent(values, "LmaxConnectivityLab:MarketDataMaxWaitSeconds", "QQ_LMAX_MARKET_DATA_MAX_WAIT_SECONDS", "LmaxConnectivityLab__MarketDataMaxWaitSeconds");
        SetIfPresent(values, "LmaxConnectivityLab:MarketDataMaxMessages", "QQ_LMAX_MARKET_DATA_MAX_MESSAGES", "LmaxConnectivityLab__MarketDataMaxMessages");
        SetIfPresent(values, "LmaxConnectivityLab:MarketDataSymbolEncodingMode", "QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE", "LmaxConnectivityLab__MarketDataSymbolEncodingMode");
        SetIfPresent(values, "LmaxConnectivityLab:ShowFixMessages", "QQ_LMAX_SHOW_FIX_MESSAGES", "LmaxConnectivityLab__ShowFixMessages");
        SetIfPresent(values, "LmaxConnectivityLab:RequestTimeoutSeconds", "QQ_LMAX_REQUEST_TIMEOUT_SECONDS", "LmaxConnectivityLab__RequestTimeoutSeconds");
        SetIfPresent(values, "LmaxConnectivityLab:AccountApiKey", "QQ_LMAX_ACCOUNT_API_KEY", "LmaxConnectivityLab__AccountApiKey");
    }

    private static void SetIfPresent(IDictionary<string, string> values, string key, params string[] environmentNames)
    {
        foreach (var environmentName in environmentNames)
        {
            var value = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
                return;
            }
        }
    }

    private static void ApplyValues(LmaxConnectivityLabOptions options, IReadOnlyDictionary<string, string> values)
    {
        options.Enabled = GetBool(values, nameof(Enabled), options.Enabled);
        options.EnvironmentName = GetString(values, nameof(EnvironmentName), options.EnvironmentName) ?? options.EnvironmentName;
        options.AllowExternalConnections = GetBool(values, nameof(AllowExternalConnections), options.AllowExternalConnections);
        options.AllowOrderSubmission = GetBool(values, nameof(AllowOrderSubmission), options.AllowOrderSubmission);
        options.AllowLiveTrading = GetBool(values, nameof(AllowLiveTrading), options.AllowLiveTrading);
        options.DryRun = GetBool(values, nameof(DryRun), options.DryRun);
        options.VenueName = GetString(values, nameof(VenueName), options.VenueName) ?? options.VenueName;
        options.AccountCode = GetString(values, nameof(AccountCode), options.AccountCode) ?? options.AccountCode;
        options.AccountApiBaseUrl = GetString(values, nameof(AccountApiBaseUrl), options.AccountApiBaseUrl);
        options.PublicDataApiBaseUrl = GetString(values, nameof(PublicDataApiBaseUrl), options.PublicDataApiBaseUrl);
        options.FixOrderHost = GetString(values, nameof(FixOrderHost), options.FixOrderHost);
        options.FixOrderPort = GetInt(values, nameof(FixOrderPort), options.FixOrderPort);
        options.FixMarketDataHost = GetString(values, nameof(FixMarketDataHost), options.FixMarketDataHost);
        options.FixMarketDataPort = GetInt(values, nameof(FixMarketDataPort), options.FixMarketDataPort);
        options.FixSenderCompId = GetString(values, nameof(FixSenderCompId), options.FixSenderCompId);
        options.FixOrderTargetCompId = GetString(values, nameof(FixOrderTargetCompId), options.FixOrderTargetCompId);
        options.FixMarketDataTargetCompId = GetString(values, nameof(FixMarketDataTargetCompId), options.FixMarketDataTargetCompId);
        options.FixTargetCompId = GetString(values, nameof(FixTargetCompId), options.FixTargetCompId);
        options.FixUsername = GetString(values, nameof(FixUsername), options.FixUsername);
        options.FixPassword = GetString(values, nameof(FixPassword), options.FixPassword);
        options.UseTls = GetBool(values, nameof(UseTls), options.UseTls);
        options.InstrumentSymbol = GetString(values, nameof(InstrumentSymbol), options.InstrumentSymbol) ?? options.InstrumentSymbol;
        options.LmaxInstrumentId = GetString(values, nameof(LmaxInstrumentId), options.LmaxInstrumentId) ?? options.LmaxInstrumentId;
        options.LmaxSlashSymbol = GetString(values, nameof(LmaxSlashSymbol), options.LmaxSlashSymbol) ?? options.LmaxSlashSymbol;
        options.FixSecurityIdSource = GetString(values, nameof(FixSecurityIdSource), options.FixSecurityIdSource) ?? options.FixSecurityIdSource;
        options.MarketDepth = GetInt(values, nameof(MarketDepth), options.MarketDepth) ?? options.MarketDepth;
        options.MarketDataRequestMode = GetEnum(values, nameof(MarketDataRequestMode), options.MarketDataRequestMode);
        options.ConnectTimeoutSeconds = GetInt(values, nameof(ConnectTimeoutSeconds), options.ConnectTimeoutSeconds) ?? options.ConnectTimeoutSeconds;
        options.LogonTimeoutSeconds = GetInt(values, nameof(LogonTimeoutSeconds), options.LogonTimeoutSeconds) ?? options.LogonTimeoutSeconds;
        options.MarketDataMaxWaitSeconds = GetInt(values, nameof(MarketDataMaxWaitSeconds), options.MarketDataMaxWaitSeconds) ?? options.MarketDataMaxWaitSeconds;
        options.MarketDataMaxMessages = GetInt(values, nameof(MarketDataMaxMessages), options.MarketDataMaxMessages) ?? options.MarketDataMaxMessages;
        options.MarketDataSymbolEncodingMode = GetEnum(values, nameof(MarketDataSymbolEncodingMode), options.MarketDataSymbolEncodingMode);
        options.ShowFixMessages = GetBool(values, nameof(ShowFixMessages), options.ShowFixMessages);
        options.RequestTimeoutSeconds = GetInt(values, nameof(RequestTimeoutSeconds), options.RequestTimeoutSeconds) ?? options.RequestTimeoutSeconds;
        options.AccountApiKey = GetString(values, nameof(AccountApiKey), options.AccountApiKey);
    }

    private static string? GetString(IReadOnlyDictionary<string, string> values, string key, string? defaultValue)
        => values.TryGetValue($"LmaxConnectivityLab:{key}", out var value) ? value : defaultValue;

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
        => values.TryGetValue($"LmaxConnectivityLab:{key}", out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int? GetInt(IReadOnlyDictionary<string, string> values, string key, int? defaultValue)
        => values.TryGetValue($"LmaxConnectivityLab:{key}", out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static TEnum GetEnum<TEnum>(IReadOnlyDictionary<string, string> values, string key, TEnum defaultValue)
        where TEnum : struct
        => values.TryGetValue($"LmaxConnectivityLab:{key}", out var value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : defaultValue;
}

public sealed record LabCommandResult(
    string Command,
    string Status,
    string Message,
    IReadOnlyList<string> SafetyDecisions,
    string? SessionType = null,
    bool? Connected = null,
    bool? LoggedOn = null,
    DateTimeOffset? StartedAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null)
{
    public bool IsSuccess => Status is "Ok" or "Skipped";
    public static LabCommandResult Ok(string command, string message, IReadOnlyList<string> decisions) => new(command, "Ok", message, decisions);
    public static LabCommandResult Skipped(string command, string message, IReadOnlyList<string> decisions) => new(command, "Skipped", message, decisions);
    public static LabCommandResult Blocked(string command, string message, IReadOnlyList<string> decisions) => new(command, "Blocked", message, decisions);
    public static LabCommandResult Failed(string command, string message, IReadOnlyList<string> decisions) => new(command, "Failed", message, decisions);
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
    Task<LabCommandResult> LogonSmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken);
    Task<LmaxFixMarketDataSmokeResult> MarketDataSnapshotSmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken);
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

    public Task<LabCommandResult> LogonSmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
    {
        var command = marketData ? "fix-marketdata-logon-smoke" : "fix-order-logon-smoke";
        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForFixLogon(options, marketData).ToList();
        if (issues.Count > 0)
        {
            return Task.FromResult(new LabCommandResult(command, "Skipped", string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), marketData ? "MarketData" : "Order", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        return Task.FromResult(new LabCommandResult(command, "Skipped", "No FIX session implementation is wired into this client.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), marketData ? "MarketData" : "Order", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }

    public Task<LmaxFixMarketDataSmokeResult> MarketDataSnapshotSmokeAsync(LmaxConnectivityLabOptions options, CancellationToken cancellationToken)
        => Task.FromResult(LmaxFixMarketDataSmokeResult.Skipped("Read-only market data snapshot smoke is not implemented in this placeholder client.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options)));

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
