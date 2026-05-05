namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public interface ILmaxAccountApiClient
{
    LabCommandResult CheckConfig(LmaxConnectivityLabOptions options);
    Task<LmaxAccountApiSmokeResult> DiscoverAsync(LmaxConnectivityLabOptions options, string command, IReadOnlyList<string>? endpoints, bool showResponseExcerpt, CancellationToken cancellationToken);
}

public sealed record LmaxAccountApiEndpointProbeResult(
    string Endpoint,
    string AuthMode,
    int? HttpStatus,
    string Status,
    string? ContentType,
    string? Excerpt,
    IReadOnlyList<string> TopLevelFields,
    int? ItemCount,
    string Message);

public sealed record LmaxAccountApiAuthAttemptResult(string AuthMode, bool Attempted, string Status, string Message);

public sealed record LmaxAccountApiSmokeResult(
    string Command,
    string Status,
    string Message,
    string BaseUrl,
    IReadOnlyList<string> SafetyDecisions,
    IReadOnlyList<LmaxAccountApiAuthAttemptResult> AuthAttempts,
    IReadOnlyList<LmaxAccountApiEndpointProbeResult> EndpointProbes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public static LmaxAccountApiSmokeResult Skipped(string command, string message, LmaxConnectivityLabOptions options, IReadOnlyList<string> decisions)
    {
        var now = DateTimeOffset.UtcNow;
        return new(command, "Skipped", message, options.AccountApiBaseUrl ?? "(not configured)", decisions, [], [], now, now);
    }
}

public sealed class LmaxAccountApiClient(
    LmaxConnectivityLabSafetyValidator safety,
    HttpMessageHandler? handler = null) : ILmaxAccountApiClient
{
    public static readonly IReadOnlyList<string> DefaultDiscoveryEndpoints =
    [
        "/",
        "/openapi.json",
        "/swagger/v1/swagger.json",
        "/account",
        "/accounts",
        "/v1/account",
        "/v1/accounts",
        "/v1/account/summary",
        "/v1/account/positions",
        "/v1/account/wallets",
        "/v1/account/balances",
        "/v1/account/open-orders",
        "/v1/account/trades",
        "/v1/account/executions",
        "/working-orders",
        "/order-positions",
        "/instrument-positions",
        "/wallets",
        "/wallet-balances",
        "/trade-history",
        "/instrument-data"
    ];

    public static readonly IReadOnlyList<string> PositionEndpoints = ["/v1/account/positions", "/order-positions", "/instrument-positions"];
    public static readonly IReadOnlyList<string> BalanceEndpoints = ["/v1/account/wallets", "/v1/account/balances", "/wallets", "/wallet-balances"];
    public static readonly IReadOnlyList<string> OpenOrderEndpoints = ["/v1/account/open-orders", "/working-orders"];
    public static readonly IReadOnlyList<string> TradeHistoryEndpoints = ["/v1/account/trades", "/v1/account/executions", "/trade-history"];

    public LabCommandResult CheckConfig(LmaxConnectivityLabOptions options)
    {
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options)
            .Concat([
                $"AccountApiBaseUrl={options.AccountApiBaseUrl ?? "(not configured)"}",
                $"AccountApiAuthMode={options.AccountApiAuthMode}",
                $"BasicAuthConfigured={HasBasic(options)}",
                $"BearerConfigured={HasBearer(options)}",
                $"HeaderApiKeyConfigured={HasHeaderKey(options)}"
            ])
            .ToList();
        var issues = safety.ValidateForAccountApi(options).Where(x => !x.Contains("AllowExternalConnections=false", StringComparison.Ordinal)).ToList();
        if (issues.Count > 0) return LabCommandResult.Skipped("account-api-config-check", string.Join(" ", issues), decisions);
        return LabCommandResult.Ok("account-api-config-check", "Account API configuration is structurally valid. No network call was made.", decisions);
    }

    public async Task<LmaxAccountApiSmokeResult> DiscoverAsync(LmaxConnectivityLabOptions options, string command, IReadOnlyList<string>? endpoints, bool showResponseExcerpt, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var decisions = LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options).ToList();
        var issues = safety.ValidateForAccountApi(options);
        if (issues.Count > 0)
        {
            return new(command, "Skipped", string.Join(" ", issues), options.AccountApiBaseUrl ?? "(not configured)", decisions, [], [], started, DateTimeOffset.UtcNow);
        }

        var authModes = ResolveAuthModes(options).ToList();
        if (authModes.Count == 0)
        {
            return new(command, "Skipped", "No Account API auth credentials are configured for the selected auth mode.", options.AccountApiBaseUrl!, decisions, [], [], started, DateTimeOffset.UtcNow);
        }

        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(options.AccountApiBaseUrl!);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AccountApiRequestTimeoutSeconds));

        var authAttempts = new List<LmaxAccountApiAuthAttemptResult>();
        var probes = new List<LmaxAccountApiEndpointProbeResult>();
        var probeEndpoints = (endpoints is { Count: > 0 } ? endpoints : DefaultDiscoveryEndpoints).Take(25).ToList();
        foreach (var mode in authModes)
        {
            authAttempts.Add(new(mode.ToString(), true, "Started", "Read-only GET probes only."));
            foreach (var endpoint in probeEndpoints)
            {
                var probe = await ProbeAsync(client, options, mode, endpoint, showResponseExcerpt, cancellationToken);
                probes.Add(probe);
                if (probe.HttpStatus is >= 200 and < 300)
                {
                    authAttempts[^1] = authAttempts[^1] with { Status = "Succeeded", Message = $"Reachable endpoint {endpoint}." };
                    return new(command, "Ok", $"Account API read-only probe succeeded at {endpoint} using {mode}.", options.AccountApiBaseUrl!, decisions, authAttempts, probes, started, DateTimeOffset.UtcNow);
                }
            }

            authAttempts[^1] = authAttempts[^1] with { Status = "NoReachableEndpoint", Message = "No reachable endpoint found for this auth mode." };
        }

        var authFailures = probes.Count(x => x.HttpStatus is 401 or 403);
        var status = authFailures > 0 ? "Failed" : "Skipped";
        var message = authFailures > 0
            ? "Account API probes reached the host but authentication/permission failed."
            : "Account API discovery completed without a reachable known endpoint.";
        return new(command, status, message, options.AccountApiBaseUrl!, decisions, authAttempts, probes, started, DateTimeOffset.UtcNow);
    }

    private async Task<LmaxAccountApiEndpointProbeResult> ProbeAsync(HttpClient client, LmaxConnectivityLabOptions options, LmaxAccountApiAuthMode mode, string endpoint, bool showResponseExcerpt, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            ApplyAuth(request, options, mode);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var excerpt = showResponseExcerpt ? SafeExcerpt(body) : null;
            var (fields, count) = SummarizeJson(body);
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Unauthorized.",
                HttpStatusCode.Forbidden => "Forbidden.",
                HttpStatusCode.NotFound => "Not found.",
                _ when (int)response.StatusCode >= 200 && (int)response.StatusCode < 300 => "Reachable.",
                _ => response.ReasonPhrase ?? response.StatusCode.ToString()
            };
            return new(endpoint, mode.ToString(), (int)response.StatusCode, StatusFor(response.StatusCode), contentType, excerpt, fields, count, message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new(endpoint, mode.ToString(), null, "Failed", null, null, [], null, Sanitize(ex.Message));
        }
    }

    public static IReadOnlyList<LmaxAccountApiAuthMode> ResolveAuthModes(LmaxConnectivityLabOptions options)
    {
        if (options.AccountApiAuthMode == LmaxAccountApiAuthMode.Auto)
        {
            var modes = new List<LmaxAccountApiAuthMode>();
            if (HasBasic(options)) modes.Add(LmaxAccountApiAuthMode.BasicAuth);
            if (HasBearer(options)) modes.Add(LmaxAccountApiAuthMode.BearerApiKey);
            if (HasHeaderKey(options)) modes.Add(LmaxAccountApiAuthMode.HeaderApiKey);
            return modes;
        }

        return options.AccountApiAuthMode switch
        {
            LmaxAccountApiAuthMode.None => [LmaxAccountApiAuthMode.None],
            LmaxAccountApiAuthMode.BasicAuth when HasBasic(options) => [LmaxAccountApiAuthMode.BasicAuth],
            LmaxAccountApiAuthMode.BearerApiKey when HasBearer(options) => [LmaxAccountApiAuthMode.BearerApiKey],
            LmaxAccountApiAuthMode.HeaderApiKey when HasHeaderKey(options) => [LmaxAccountApiAuthMode.HeaderApiKey],
            LmaxAccountApiAuthMode.UsernamePasswordForm => [],
            _ => []
        };
    }

    public static string BuildMaskedAuthSummary(LmaxConnectivityLabOptions options, LmaxAccountApiAuthMode mode)
        => mode switch
        {
            LmaxAccountApiAuthMode.BasicAuth => $"Authorization=Basic {LmaxConnectivityLabOptions.Mask(options.AccountApiUsername)}:{LmaxConnectivityLabOptions.Mask(options.AccountApiPassword)}",
            LmaxAccountApiAuthMode.BearerApiKey => "Authorization=Bearer ********",
            LmaxAccountApiAuthMode.HeaderApiKey => $"{options.AccountApiKeyHeaderName}=********",
            LmaxAccountApiAuthMode.None => "No auth header",
            _ => "Skipped"
        };

    public static void ApplyAuth(HttpRequestMessage request, LmaxConnectivityLabOptions options, LmaxAccountApiAuthMode mode)
    {
        if (mode == LmaxAccountApiAuthMode.BasicAuth)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountApiUsername}:{options.AccountApiPassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        else if (mode == LmaxAccountApiAuthMode.BearerApiKey)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccountApiBearerToken ?? options.AccountApiKey);
        }
        else if (mode == LmaxAccountApiAuthMode.HeaderApiKey)
        {
            request.Headers.TryAddWithoutValidation(options.AccountApiKeyHeaderName, options.AccountApiKey);
        }
    }

    private static bool HasBasic(LmaxConnectivityLabOptions options) => !string.IsNullOrWhiteSpace(options.AccountApiUsername) && !string.IsNullOrWhiteSpace(options.AccountApiPassword);
    private static bool HasBearer(LmaxConnectivityLabOptions options) => !string.IsNullOrWhiteSpace(options.AccountApiBearerToken) || !string.IsNullOrWhiteSpace(options.AccountApiKey);
    private static bool HasHeaderKey(LmaxConnectivityLabOptions options) => !string.IsNullOrWhiteSpace(options.AccountApiKey);
    private static string StatusFor(HttpStatusCode code) => code switch { HttpStatusCode.NotFound => "NotFound", HttpStatusCode.Unauthorized => "Unauthorized", HttpStatusCode.Forbidden => "Forbidden", _ when (int)code >= 200 && (int)code < 300 => "Reachable", _ => "HttpError" };

    private static (IReadOnlyList<string> Fields, int? Count) SummarizeJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return ([], null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return (doc.RootElement.EnumerateObject().Select(x => x.Name).Take(20).ToList(), null);
            if (doc.RootElement.ValueKind == JsonValueKind.Array) return ([], doc.RootElement.GetArrayLength());
        }
        catch (JsonException) { }
        return ([], null);
    }

    private static string SafeExcerpt(string body)
        => Sanitize(body.Length <= 500 ? body : body[..500]);

    private static string Sanitize(string value)
        => value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
