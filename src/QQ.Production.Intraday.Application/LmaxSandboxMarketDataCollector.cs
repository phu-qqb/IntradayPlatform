using System.Globalization;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxSandboxMarketDataOptions(
    string RunKey,
    string OutputRoot,
    string Environment,
    string Host,
    int Port,
    string TargetCompId,
    string SenderCompId,
    string? UsernameSecretRef,
    string? PasswordSecretRef,
    IReadOnlyList<string> Instruments,
    int DurationSeconds,
    int MaxMessages,
    int ConnectTimeoutMs,
    int ReadTimeoutMs,
    int HeartbeatIntervalSeconds,
    int MdUpdateType,
    bool NoExternal,
    bool ExternalApproved,
    string? OperatorApprovalPhrase,
    bool NoExecution,
    bool DisableOrderPath,
    bool UseFakeFixServer)
{
    public const string DemoEnvironment = "demo";
    public const string DemoMarketDataHost = "fix-marketdata.london-demo.lmax.com";
    public const string DemoOrderHost = "fix-order.london-demo.lmax.com";
    public const string DemoMarketDataTargetCompId = "LMXBDM";
    public const string DemoOrderTargetCompId = "LMXBD";
    public const string ApprovalPhrase = "APPROVE_LMAX_DEMO_READONLY_MARKETDATA";

    public static LmaxSandboxMarketDataOptions CreateDefault(
        string runKey,
        string outputRoot,
        IReadOnlyList<string> instruments,
        bool noExternal = true,
        bool externalApproved = false,
        bool useFakeFixServer = true,
        string? operatorApprovalPhrase = null)
        => new(
            runKey,
            outputRoot,
            DemoEnvironment,
            DemoMarketDataHost,
            443,
            DemoMarketDataTargetCompId,
            "QQPRODMD",
            "LMAX_DEMO_MD_USERNAME",
            "LMAX_DEMO_MD_PASSWORD",
            instruments,
            DurationSeconds: 30,
            MaxMessages: 100,
            ConnectTimeoutMs: 10_000,
            ReadTimeoutMs: 2_000,
            HeartbeatIntervalSeconds: 30,
            MdUpdateType: 0,
            noExternal,
            externalApproved,
            operatorApprovalPhrase,
            NoExecution: true,
            DisableOrderPath: true,
            useFakeFixServer);
}

public sealed record LmaxSandboxInstrumentMapping(
    string Instrument,
    string SecurityId,
    string SecurityIdSource,
    string MappingSource,
    string MappingConfidence)
{
    public static LmaxSandboxInstrumentMapping? Find(string instrument)
    {
        if (string.Equals(instrument, "GBPUSD", StringComparison.OrdinalIgnoreCase))
        {
            return new("GBPUSD", "4002", "8", "LMAX_READONLY_APPROVED_INSTRUMENT_ALLOWLIST", "HIGH");
        }

        if (string.Equals(instrument, "EURGBP", StringComparison.OrdinalIgnoreCase))
        {
            return new("EURGBP", "4003", "8", "LMAX_READONLY_APPROVED_INSTRUMENT_ALLOWLIST", "HIGH");
        }

        return null;
    }
}

public sealed record LmaxSandboxMarketDataPreflightReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string LmaxSandboxPreflightGate,
    bool EnvironmentDemo,
    bool MarketDataEndpointOnly,
    bool TradingEndpointBlocked,
    bool TargetCompIdMarketDataOnly,
    bool NoExecution,
    bool OrderPathDisabled,
    bool DurationBounded,
    bool MaxMessagesBounded,
    bool InstrumentsAllowListed,
    bool DbPersistenceDisabled,
    bool OutputRootRunKeyValid,
    bool SecretsNotExposed,
    bool CredentialsAvailable,
    bool ExternalApprovalValid,
    int MdUpdateType,
    bool MdUpdateTypeValid,
    string MdUpdateTypeGate,
    bool NoExternal,
    bool UseFakeFixServer,
    IReadOnlyList<LmaxSandboxInstrumentMapping> InstrumentMappings,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings);

public static class LmaxSandboxMarketDataPreflight
{
    public static LmaxSandboxMarketDataPreflightReport Evaluate(LmaxSandboxMarketDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var warnings = new List<string>();
        var mappings = new List<LmaxSandboxInstrumentMapping>();

        var environmentDemo = string.Equals(options.Environment, LmaxSandboxMarketDataOptions.DemoEnvironment, StringComparison.OrdinalIgnoreCase);
        Require(environmentDemo, "environment must be demo", failures);

        var marketDataEndpointOnly = string.Equals(options.Host, LmaxSandboxMarketDataOptions.DemoMarketDataHost, StringComparison.OrdinalIgnoreCase) &&
                                     !options.Host.Contains("fix-order", StringComparison.OrdinalIgnoreCase) &&
                                     options.Port == 443;
        Require(marketDataEndpointOnly, "host must be demo market-data endpoint on port 443", failures);

        var tradingEndpointBlocked = !options.Host.Contains("fix-order", StringComparison.OrdinalIgnoreCase) &&
                                     !string.Equals(options.Host, LmaxSandboxMarketDataOptions.DemoOrderHost, StringComparison.OrdinalIgnoreCase);
        Require(tradingEndpointBlocked, "trading endpoint is forbidden", failures);

        var targetCompIdMarketDataOnly = string.Equals(options.TargetCompId, LmaxSandboxMarketDataOptions.DemoMarketDataTargetCompId, StringComparison.Ordinal) &&
                                         !string.Equals(options.TargetCompId, LmaxSandboxMarketDataOptions.DemoOrderTargetCompId, StringComparison.Ordinal);
        Require(targetCompIdMarketDataOnly, "TargetCompId must be LMXBDM and must not be LMXBD", failures);

        Require(options.NoExecution, "NoExecution must be true", failures);
        Require(options.DisableOrderPath, "DisableOrderPath must be true", failures);
        var durationBounded = options.DurationSeconds is > 0 and <= 120;
        Require(durationBounded, "DurationSeconds must be in 1..120", failures);
        var maxMessagesBounded = options.MaxMessages is > 0 and <= 10_000;
        Require(maxMessagesBounded, "MaxMessages must be in 1..10000", failures);
        var mdUpdateTypeValid = options.MdUpdateType is 0 or 1;
        Require(mdUpdateTypeValid, "md-update-type must be 0 or 1", failures);
        if (options.MdUpdateType == 1)
        {
            warnings.Add("md-update-type 1 was explicitly requested; last LMAX demo run rejected tag 265 when value was 1");
        }

        foreach (var instrument in options.Instruments)
        {
            var mapping = LmaxSandboxInstrumentMapping.Find(instrument);
            if (mapping is null)
            {
                failures.Add($"instrument mapping unavailable: {instrument}");
            }
            else
            {
                mappings.Add(mapping);
                if (string.IsNullOrWhiteSpace(mapping.SecurityId))
                {
                    failures.Add($"instrument mapping missing SecurityID: {instrument}");
                }

                if (string.IsNullOrWhiteSpace(mapping.SecurityIdSource))
                {
                    failures.Add($"instrument mapping missing SecurityIDSource: {instrument}");
                }
            }
        }

        if (options.Instruments.Count != 1)
        {
            failures.Add("LMAX demo market-data collector currently supports exactly one instrument per MarketDataRequest.");
        }

        var instrumentsAllowListed = options.Instruments.Count == 1 &&
                                     mappings.Count == options.Instruments.Count &&
                                     mappings.All(x => !string.IsNullOrWhiteSpace(x.SecurityId) && !string.IsNullOrWhiteSpace(x.SecurityIdSource));
        var outputRootRunKeyValid = !string.IsNullOrWhiteSpace(options.RunKey) &&
                                    !string.IsNullOrWhiteSpace(options.OutputRoot) &&
                                    Path.GetFileName(Path.GetFullPath(options.OutputRoot)).Equals(options.RunKey, StringComparison.OrdinalIgnoreCase);
        Require(outputRootRunKeyValid, "output-root leaf must match run-key", failures);

        var secretsNotExposed = SecretRefSafe(options.UsernameSecretRef) && SecretRefSafe(options.PasswordSecretRef);
        Require(secretsNotExposed, "credentials must be secret refs only; raw credential-shaped values are forbidden", failures);
        var credentialsAvailable = options.NoExternal ||
                                   !string.IsNullOrWhiteSpace(ReadSecretRef(options.UsernameSecretRef)) &&
                                   !string.IsNullOrWhiteSpace(ReadSecretRef(options.PasswordSecretRef));
        Require(credentialsAvailable, "external sandbox capture requires username/password secret refs to resolve from environment", failures);

        var externalApprovalValid = !options.ExternalApproved ||
                                    string.Equals(options.OperatorApprovalPhrase, LmaxSandboxMarketDataOptions.ApprovalPhrase, StringComparison.Ordinal);
        Require(externalApprovalValid, "external approved runs require exact operator approval phrase", failures);

        if (options.NoExternal && !options.UseFakeFixServer)
        {
            failures.Add("no-external runs must use the fake FIX server transport");
        }

        if (!options.NoExternal && !options.ExternalApproved)
        {
            failures.Add("external sandbox capture requires ExternalApproved=true");
        }

        if (!options.NoExternal && options.UseFakeFixServer)
        {
            warnings.Add("external flag is false but fake transport requested; no external capture will be attempted");
        }

        return new LmaxSandboxMarketDataPreflightReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            failures.Count == 0 ? "PASS" : "FAIL",
            environmentDemo,
            marketDataEndpointOnly,
            tradingEndpointBlocked,
            targetCompIdMarketDataOnly,
            options.NoExecution,
            options.DisableOrderPath,
            durationBounded,
            maxMessagesBounded,
            instrumentsAllowListed,
            DbPersistenceDisabled: true,
            outputRootRunKeyValid,
            secretsNotExposed,
            credentialsAvailable,
            externalApprovalValid,
            options.MdUpdateType,
            mdUpdateTypeValid,
            options.MdUpdateType == 1 ? "WARN" : mdUpdateTypeValid ? "PASS" : "FAIL",
            options.NoExternal,
            options.UseFakeFixServer,
            mappings,
            failures,
            warnings);
    }

    private static void Require(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static bool SecretRefSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return !value.Contains('=') &&
               !value.Contains("password:", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("secret:", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("554=", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadSecretRef(string? secretRef)
        => string.IsNullOrWhiteSpace(secretRef) ? null : Environment.GetEnvironmentVariable(secretRef);
}

public interface IFixTransport
{
    string Source { get; }
    bool ExternalCallAttempted { get; }
    Task ConnectAsync(LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken);
    Task SendAsync(string redactedFixMessage, CancellationToken cancellationToken);
    Task<string?> ReadAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}

public sealed class FakeFixTransport : IFixTransport
{
    private readonly Queue<string> frames;
    private bool connected;

    public FakeFixTransport(IEnumerable<string>? frames = null)
    {
        this.frames = new Queue<string>(frames ?? DefaultFrames());
    }

    public string Source => "fake";
    public bool ExternalCallAttempted => false;
    public IReadOnlyList<string> SentMessages { get; private set; } = [];

    public Task ConnectAsync(LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.NoExternal)
        {
            throw new InvalidOperationException("FakeFixTransport is intended for no-external validation runs.");
        }

        connected = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(string redactedFixMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!connected)
        {
            throw new InvalidOperationException("FakeFixTransport is not connected.");
        }

        SentMessages = SentMessages.Concat([redactedFixMessage]).ToArray();
        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(frames.Count == 0 ? null : frames.Dequeue());
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connected = false;
        return Task.CompletedTask;
    }

    private static IEnumerable<string> DefaultFrames()
    {
        yield return FixFields("35=W", "55=GBPUSD", "48=4002", "22=8", "268=2", "269=0", "270=1.27001", "271=1000000", "269=1", "270=1.27003", "271=1000000");
        yield return FixFields("35=X", "55=GBPUSD", "48=4002", "22=8", "268=2", "269=0", "270=1.27002", "271=900000", "269=1", "270=1.27004", "271=950000");
        yield return FixFields("35=Y", "262=QQPRODMD-GBPUSD", "281=0", "58=FAKE_READONLY_REJECT_FOR_CLASSIFICATION");
    }

    public static string FixFields(params string[] fields)
        => string.Join('\u0001', fields) + '\u0001';

    public static string FixMessage(params string[] fields)
        => FixFields(fields);
}

public sealed class TlsTcpFixTransport : IFixTransport
{
    private TcpClient? client;
    private SslStream? sslStream;
    private bool externalCallAttempted;

    public string Source => "lmax-demo";
    public bool ExternalCallAttempted => externalCallAttempted;

    public async Task ConnectAsync(LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.NoExternal)
        {
            throw new InvalidOperationException("External transport blocked because NoExternal=true.");
        }

        if (!options.ExternalApproved ||
            !string.Equals(options.OperatorApprovalPhrase, LmaxSandboxMarketDataOptions.ApprovalPhrase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("External transport requires exact operator approval.");
        }

        externalCallAttempted = true;
        client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.ConnectTimeoutMs);
        await client.ConnectAsync(options.Host, options.Port, timeoutCts.Token);
        sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsClientAsync(options.Host, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: true);
    }

    public async Task SendAsync(string redactedFixMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sslStream is null)
        {
            throw new InvalidOperationException("TLS FIX transport is not connected.");
        }

        var bytes = Encoding.ASCII.GetBytes(redactedFixMessage);
        await sslStream.WriteAsync(bytes, cancellationToken);
        await sslStream.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sslStream is null)
        {
            throw new InvalidOperationException("TLS FIX transport is not connected.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var buffer = new byte[8192];
        try
        {
            var read = await sslStream.ReadAsync(buffer, timeoutCts.Token);
            return read <= 0 ? null : Encoding.ASCII.GetString(buffer, 0, read);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sslStream?.Dispose();
        client?.Dispose();
        return Task.CompletedTask;
    }
}

public static class LmaxMarketDataFixMessagePolicy
{
    private static readonly HashSet<string> AllowedMarketDataMessages = new(StringComparer.Ordinal)
    {
        "0", "1", "5", "A", "V"
    };

    private static readonly HashSet<string> ForbiddenTradingMessages = new(StringComparer.Ordinal)
    {
        "D", "F", "G", "8", "AE", "H"
    };

    public static LmaxFixPolicyDecision ValidateOutgoing(string fixMessage, LmaxSandboxMarketDataOptions options)
    {
        var msgType = LmaxFixMessageClassifier.GetField(fixMessage, "35");
        if (string.IsNullOrWhiteSpace(msgType))
        {
            return new(false, "MissingMsgType", "Outgoing FIX message has no 35 tag.");
        }

        if (ForbiddenTradingMessages.Contains(msgType))
        {
            return new(false, "TradingMessageForbidden", $"Outgoing FIX message type 35={msgType} is forbidden in market-data collector.");
        }

        if (msgType == "A" && (!options.ExternalApproved ||
                               !string.Equals(options.OperatorApprovalPhrase, LmaxSandboxMarketDataOptions.ApprovalPhrase, StringComparison.Ordinal)))
        {
            return new(false, "LogonRequiresExternalApproval", "FIX Logon is allowed only for operator-approved external sandbox capture.");
        }

        return AllowedMarketDataMessages.Contains(msgType)
            ? new(true, "AllowedMarketDataMessage", $"Outgoing FIX message type 35={msgType} is allowed.")
            : new(false, "UnknownOutgoingMessageForbidden", $"Outgoing FIX message type 35={msgType} is not on the market-data allow-list.");
    }
}

public sealed record LmaxFixPolicyDecision(bool Allowed, string Category, string Message);

public sealed record LmaxFixRejectDetails(
    string? RefTagId,
    string? RefMsgType,
    string? Text,
    string? ReasonCode);

public sealed record LmaxFixClassification(
    string FixMsgType,
    string Classification,
    string? Instrument,
    LmaxFixRejectDetails? RejectDetails,
    decimal? Bid,
    decimal? Ask,
    decimal? BidSize,
    decimal? AskSize,
    string? RefTagId,
    string? RefMsgType,
    string? RejectText,
    string RawRedactedFix);

public static class LmaxFixMessageClassifier
{
    public static LmaxFixClassification Classify(string? frame)
    {
        if (string.IsNullOrWhiteSpace(frame))
        {
            return new("TIMEOUT", "Timeout", null, null, null, null, null, null, null, null, null, string.Empty);
        }

        if (!frame.Contains("35=", StringComparison.Ordinal))
        {
            return new("MALFORMED", "Malformed frame", null, null, null, null, null, null, null, null, null, Redact(frame));
        }

        var msgType = GetField(frame, "35") ?? "UNKNOWN";
        var text = GetField(frame, "58");
        var classification = msgType switch
        {
            "W" => HasEntries(frame) ? "Snapshot" : "No entries",
            "X" => HasEntries(frame) ? "Incremental" : "No entries",
            "Y" => "Reject",
            "3" => "Reject",
            "A" => "Logon",
            "5" when string.Equals(text, "BAD_CREDENTIALS", StringComparison.OrdinalIgnoreCase) => "CredentialsRejected",
            "5" => "Logout",
            _ => "Unknown"
        };

        var rejectDetails = ExtractRejectDetails(frame);
        var (bid, ask, bidSize, askSize) = ExtractBook(frame);
        return new(
            msgType,
            classification,
            GetField(frame, "55"),
            rejectDetails,
            bid,
            ask,
            bidSize,
            askSize,
            rejectDetails?.RefTagId,
            rejectDetails?.RefMsgType,
            rejectDetails?.Text,
            Redact(frame));
    }

    public static string? GetField(string fixMessage, string tag)
    {
        foreach (var part in Split(fixMessage))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (string.Equals(part[..separator], tag, StringComparison.Ordinal))
            {
                return part[(separator + 1)..];
            }
        }

        return null;
    }

    public static IReadOnlyList<string> GetFields(string fixMessage, string tag)
        => Split(fixMessage)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2 && string.Equals(parts[0], tag, StringComparison.Ordinal))
            .Select(parts => parts[1])
            .ToArray();

    public static IReadOnlyList<string> GetTagOrder(string fixMessage)
        => Split(fixMessage)
            .Select(part => part.Split('=', 2)[0])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();

    public static string Redact(string fixMessage)
    {
        var parts = Split(fixMessage)
        .Select(part => part.StartsWith("554=", StringComparison.Ordinal) ? "554=[redacted]" : part)
            .Select(part => part.StartsWith("553=", StringComparison.Ordinal) ? "553=[redacted]" : part)
            .Select(part => part.Contains("password", StringComparison.OrdinalIgnoreCase) ? "[redacted]" : part);
        var redacted = string.Join("|", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        foreach (var secretRef in new[] { "LMAX_DEMO_MD_USERNAME", "LMAX_DEMO_MD_PASSWORD", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD" })
        {
            var secret = Environment.GetEnvironmentVariable(secretRef);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                redacted = redacted.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }
        }

        return redacted;
    }

    private static bool HasEntries(string frame)
        => GetField(frame, "268") is not null || GetField(frame, "270") is not null;

    private static LmaxFixRejectDetails? ExtractRejectDetails(string frame)
    {
        var msgType = GetField(frame, "35");
        if (msgType is not ("3" or "Y"))
        {
            return null;
        }

        return new LmaxFixRejectDetails(GetField(frame, "371"), GetField(frame, "372"), GetField(frame, "58"), GetField(frame, "373"));
    }

    private static (decimal? Bid, decimal? Ask, decimal? BidSize, decimal? AskSize) ExtractBook(string frame)
    {
        decimal? bid = null;
        decimal? ask = null;
        decimal? bidSize = null;
        decimal? askSize = null;
        var parts = Split(frame).ToArray();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "269=0")
            {
                bid = FindDecimal(parts, i, "270");
                bidSize = FindDecimal(parts, i, "271");
            }
            else if (parts[i] == "269=1")
            {
                ask = FindDecimal(parts, i, "270");
                askSize = FindDecimal(parts, i, "271");
            }
        }

        return (bid, ask, bidSize, askSize);
    }

    private static decimal? FindDecimal(string[] parts, int startIndex, string tag)
    {
        for (var i = startIndex + 1; i < parts.Length && i <= startIndex + 4; i++)
        {
            if (!parts[i].StartsWith(tag + "=", StringComparison.Ordinal))
            {
                continue;
            }

            return decimal.TryParse(parts[i][(tag.Length + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        return null;
    }

    private static IEnumerable<string> Split(string fixMessage)
        => fixMessage.Split(['\u0001', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record LmaxSandboxMarketDataEvent(
    string RunKey,
    DateTimeOffset ReceivedAtUtc,
    string Environment,
    string Source,
    string? Instrument,
    string FixMsgType,
    string Classification,
    decimal? Bid,
    decimal? Ask,
    decimal? BidSize,
    decimal? AskSize,
    string? RefTagId,
    string? RefMsgType,
    string? RejectText,
    string RawRedactedFix);

public sealed record LmaxSandboxMarketDataSummary(
    string RunKey,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    int DurationSeconds,
    string Source,
    IReadOnlyList<string> Instruments,
    int MessagesRead,
    int Snapshots,
    int Incrementals,
    int Rejects,
    int Timeouts,
    int Malformed,
    int Unknown,
    int LogonCount,
    int SessionRejectCount,
    int MarketDataRequestRejectCount,
    int LogoutCount,
    int CredentialsRejectedCount,
    string? RefTagId,
    string? RefMsgType,
    string PrimaryFailureReason,
    string FinalFixSessionState,
    int TimeoutAfterPrimaryFailureCount,
    int TerminalTimeoutCount,
    bool BoundedCaptureEndedCleanly,
    DateTimeOffset? FirstReceivedAtUtc,
    DateTimeOffset? LastReceivedAtUtc,
    string Status);

public sealed record LmaxSandboxMarketDataRunResult(
    LmaxSandboxMarketDataPreflightReport Preflight,
    LmaxSandboxMarketDataSummary Summary,
    IReadOnlyList<LmaxSandboxMarketDataEvent> Events,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings,
    bool ExternalCallsAttempted);

public sealed class LmaxSandboxMarketDataCollector
{
    private readonly IFixTransport transport;
    private readonly TimeProvider timeProvider;

    public LmaxSandboxMarketDataCollector(IFixTransport transport, TimeProvider? timeProvider = null)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LmaxSandboxMarketDataRunResult> RunAsync(LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var preflight = LmaxSandboxMarketDataPreflight.Evaluate(options);
        var started = timeProvider.GetUtcNow();
        var failures = new List<string>(preflight.Failures);
        var warnings = new List<string>(preflight.Warnings);
        var events = new List<LmaxSandboxMarketDataEvent>();

        if (preflight.LmaxSandboxPreflightGate == "PASS")
        {
            try
            {
                await transport.ConnectAsync(options, cancellationToken);

                if (!options.NoExternal && options.ExternalApproved)
                {
                    await SendPolicyCheckedAsync(BuildLogon(options), options, cancellationToken);
                }

                await SendPolicyCheckedAsync(BuildMarketDataRequest(options, preflight.InstrumentMappings), options, cancellationToken);

                var deadline = started.Add(TimeSpan.FromSeconds(options.DurationSeconds));
                while (events.Count < options.MaxMessages && timeProvider.GetUtcNow() < deadline)
                {
                    var frame = await transport.ReadAsync(TimeSpan.FromMilliseconds(options.ReadTimeoutMs), cancellationToken);
                    if (frame is null)
                    {
                        warnings.Add("bounded read timeout or fake transport exhausted");
                        if (!options.UseFakeFixServer)
                        {
                            var timeout = LmaxFixMessageClassifier.Classify(null);
                            var timeoutAtUtc = timeProvider.GetUtcNow();
                            events.Add(new LmaxSandboxMarketDataEvent(
                                options.RunKey,
                                timeoutAtUtc,
                                options.Environment,
                                transport.Source,
                                preflight.InstrumentMappings.FirstOrDefault()?.Instrument,
                                timeout.FixMsgType,
                                timeout.Classification,
                                null,
                                null,
                                null,
                                null,
                                timeout.RefTagId,
                                timeout.RefMsgType,
                                timeout.RejectText,
                                timeout.RawRedactedFix));
                        }

                        break;
                    }

                    var classification = LmaxFixMessageClassifier.Classify(frame);
                    var receivedAtUtc = timeProvider.GetUtcNow();
                    events.Add(new LmaxSandboxMarketDataEvent(
                        options.RunKey,
                        receivedAtUtc,
                        options.Environment,
                        transport.Source,
                        classification.Instrument ?? preflight.InstrumentMappings.FirstOrDefault()?.Instrument,
                        classification.FixMsgType,
                        classification.Classification,
                        classification.Bid,
                        classification.Ask,
                        classification.BidSize,
                        classification.AskSize,
                        classification.RefTagId,
                        classification.RefMsgType,
                        classification.RejectText,
                        classification.RawRedactedFix));
                }

                await SendPolicyCheckedAsync(BuildLogout(options), options, cancellationToken);
            }
            catch (Exception ex)
            {
                failures.Add(SanitizeFailure(ex.Message));
            }
            finally
            {
                await transport.CloseAsync(cancellationToken);
            }
        }

        var ended = timeProvider.GetUtcNow();
        var summary = BuildSummary(options, preflight, transport.Source, started, ended, events, failures);
        return new LmaxSandboxMarketDataRunResult(preflight, summary, events, failures, warnings, transport.ExternalCallAttempted);
    }

    private async Task SendPolicyCheckedAsync(string fixMessage, LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken)
    {
        var decision = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(fixMessage, options);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }

        await transport.SendAsync(fixMessage, cancellationToken);
    }

    public static string BuildMarketDataRequest(LmaxSandboxMarketDataOptions options, IReadOnlyList<LmaxSandboxInstrumentMapping> mappings)
    {
        if (mappings.Count != 1)
        {
            throw new InvalidOperationException("LMAX demo market-data request requires exactly one instrument mapping.");
        }

        var mapping = mappings[0];
        if (string.IsNullOrWhiteSpace(mapping.SecurityId))
        {
            throw new InvalidOperationException("LMAX demo market-data request requires SecurityID tag 48.");
        }

        if (string.IsNullOrWhiteSpace(mapping.SecurityIdSource))
        {
            throw new InvalidOperationException("LMAX demo market-data request requires SecurityIDSource tag 22.");
        }

        var fields = new List<string>
        {
            "35=V",
            $"49={EffectiveSenderCompId(options)}",
            $"56={options.TargetCompId}",
            "34=2",
            $"52={DateTimeOffset.UtcNow:yyyyMMdd-HH:mm:ss.fff}",
            $"262={options.RunKey}",
            "263=1",
            "264=0",
            $"265={options.MdUpdateType}",
            "146=1",
            $"48={mapping.SecurityId}",
            $"22={mapping.SecurityIdSource}",
            "267=2",
            "269=0",
            "269=1"
        };

        return FixMessage(fields.ToArray());
    }

    public static LmaxMarketDataRequestGroupDiagnosisReport DiagnoseMarketDataRequestGroup(
        LmaxSandboxMarketDataOptions options,
        IReadOnlyList<LmaxSandboxInstrumentMapping> mappings,
        IReadOnlyList<LmaxSandboxMarketDataEvent>? events = null)
    {
        var outgoing = BuildMarketDataRequest(options, mappings);
        var tagOrder = LmaxFixMessageClassifier.GetTagOrder(outgoing);
        var tag146Index = IndexOfTag(tagOrder, "146");
        var tag267Index = IndexOfTag(tagOrder, "267");
        var hasOneInstrumentGroup = tag146Index >= 0 &&
                                    tag267Index > tag146Index &&
                                    string.Equals(LmaxFixMessageClassifier.GetField(outgoing, "146"), "1", StringComparison.Ordinal) &&
                                    TagOccursBetween(tagOrder, "48", tag146Index, tag267Index) &&
                                    TagOccursBetween(tagOrder, "22", tag146Index, tag267Index) &&
                                    !string.IsNullOrWhiteSpace(LmaxFixMessageClassifier.GetField(outgoing, "48")) &&
                                    string.Equals(LmaxFixMessageClassifier.GetField(outgoing, "22"), "8", StringComparison.Ordinal);
        var primaryReject = events?.FirstOrDefault(x => x.RefTagId is not null || x.RefMsgType is not null);
        var primaryReason = primaryReject is { RefTagId: "146", RefMsgType: "V" } &&
                            (primaryReject.RejectText?.Contains("RepeatingGroupNumInGroupMismatch", StringComparison.OrdinalIgnoreCase) ?? false)
            ? "RepeatingGroupNumInGroupMismatch"
            : primaryReject?.RejectText ?? "NONE";

        return new LmaxMarketDataRequestGroupDiagnosisReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            LmaxFixMessageClassifier.Redact(outgoing),
            tagOrder,
            ShapeOfRequestId(LmaxFixMessageClassifier.GetField(outgoing, "262")),
            LmaxFixMessageClassifier.GetField(outgoing, "263"),
            LmaxFixMessageClassifier.GetField(outgoing, "264"),
            LmaxFixMessageClassifier.GetField(outgoing, "265"),
            LmaxFixMessageClassifier.GetField(outgoing, "146"),
            string.IsNullOrWhiteSpace(LmaxFixMessageClassifier.GetField(outgoing, "48")) ? "NO" : "YES",
            LmaxFixMessageClassifier.GetField(outgoing, "48"),
            LmaxFixMessageClassifier.GetField(outgoing, "22"),
            LmaxFixMessageClassifier.GetField(outgoing, "267"),
            LmaxFixMessageClassifier.GetFields(outgoing, "269"),
            hasOneInstrumentGroup ? 1 : 0,
            tag267Index >= 0 ? tagOrder.Skip(tag267Index + 1).Count(x => x == "269") : 0,
            primaryReject?.RefTagId,
            primaryReject?.RefMsgType,
            primaryReason,
            hasOneInstrumentGroup ? "YES" : "NO");
    }

    private static string BuildLogon(LmaxSandboxMarketDataOptions options)
    {
        var username = Environment.GetEnvironmentVariable(options.UsernameSecretRef ?? string.Empty);
        var password = Environment.GetEnvironmentVariable(options.PasswordSecretRef ?? string.Empty);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("External sandbox capture requires credential secret refs to resolve from environment.");
        }

        return FixMessage(
            "35=A",
            $"49={username}",
            $"56={options.TargetCompId}",
            "34=1",
            $"52={DateTimeOffset.UtcNow:yyyyMMdd-HH:mm:ss.fff}",
            "98=0",
            $"108={options.HeartbeatIntervalSeconds}",
            $"553={username}",
            $"554={password}");
    }

    private static string BuildLogout(LmaxSandboxMarketDataOptions options)
        => FixMessage("35=5", $"49={EffectiveSenderCompId(options)}", $"56={options.TargetCompId}", "34=3", $"52={DateTimeOffset.UtcNow:yyyyMMdd-HH:mm:ss.fff}", "58=client bounded logout");

    private static LmaxSandboxMarketDataSummary BuildSummary(
        LmaxSandboxMarketDataOptions options,
        LmaxSandboxMarketDataPreflightReport preflight,
        string source,
        DateTimeOffset started,
        DateTimeOffset ended,
        IReadOnlyList<LmaxSandboxMarketDataEvent> events,
        IReadOnlyList<string> failures)
    {
        var hasMarketData = events.Any(x => x.Classification is "Snapshot" or "Incremental");
        var logonCount = events.Count(x => x.Classification == "Logon");
        var logoutCount = events.Count(x => x.Classification is "Logout" or "CredentialsRejected");
        var credentialsRejectedCount = events.Count(x => x.Classification == "CredentialsRejected");
        var rejectCount = events.Count(x => x.Classification == "Reject");
        var sessionRejectCount = events.Count(x => x.FixMsgType == "3");
        var marketDataRequestRejectCount = events.Count(x => x.Classification == "Reject" && x.RefMsgType == "V");
        var timeoutCount = events.Count(x => x.Classification == "Timeout");
        var malformedCount = events.Count(x => x.Classification == "Malformed frame");
        var firstReject = events.FirstOrDefault(x => x.RefTagId is not null || x.RefMsgType is not null);
        var mdUpdateTypeReject = events.FirstOrDefault(x =>
            x.Classification == "Reject" &&
            string.Equals(x.RefTagId, "265", StringComparison.Ordinal) &&
            string.Equals(x.RefMsgType, "V", StringComparison.Ordinal) &&
            (x.RejectText?.Contains("ValueOutOfRange", StringComparison.OrdinalIgnoreCase) ?? false));
        var relatedSymGroupReject = events.FirstOrDefault(x =>
            x.Classification == "Reject" &&
            string.Equals(x.RefTagId, "146", StringComparison.Ordinal) &&
            string.Equals(x.RefMsgType, "V", StringComparison.Ordinal) &&
            (x.RejectText?.Contains("RepeatingGroupNumInGroupMismatch", StringComparison.OrdinalIgnoreCase) ?? false));
        var primaryFailureReason = credentialsRejectedCount > 0
            ? "BAD_CREDENTIALS"
            : !preflight.CredentialsAvailable
                ? "MISSING_DEMO_MARKETDATA_SECRET_REFS"
                : mdUpdateTypeReject is not null
                    ? "MARKET_DATA_REQUEST_MD_UPDATE_TYPE_VALUE_OUT_OF_RANGE"
                    : relatedSymGroupReject is not null
                        ? "MARKET_DATA_REQUEST_RELATED_SYM_GROUP_MISMATCH"
                        : failures.Count > 0
                            ? "UNKNOWN"
                            : hasMarketData
                                ? "NONE"
                                : rejectCount > 0
                                    ? "UNKNOWN"
                                    : malformedCount > 0
                                        ? "MALFORMED"
                                        : timeoutCount > 0
                                            ? "TIMEOUT"
                                            : "NONE";
        var finalFixSessionState = credentialsRejectedCount > 0
            ? "LogoutCredentialsRejected"
            : logoutCount > 0
                ? "LogoutObserved"
                : hasMarketData
                    ? "MarketDataObserved"
                    : rejectCount > 0
                        ? "RejectObserved"
                        : timeoutCount > 0
                        ? "TimedOut"
                        : "NoMessages";
        var timeoutAfterPrimaryFailureCount = primaryFailureReason is not ("NONE" or "TIMEOUT") ? timeoutCount : 0;
        var terminalTimeoutCount = hasMarketData && rejectCount == 0 && logoutCount == 0 && malformedCount == 0 && failures.Count == 0
            ? timeoutCount
            : 0;
        var boundedCaptureEndedCleanly = hasMarketData &&
                                         primaryFailureReason == "NONE" &&
                                         rejectCount == 0 &&
                                         logoutCount == 0 &&
                                         malformedCount == 0 &&
                                         failures.Count == 0;
        var status = failures.Count > 0 || credentialsRejectedCount > 0
            ? "FAIL"
            : hasMarketData
                ? "PASS"
                : events.Count > 0
                    ? "WARN"
                    : "WARN";
        return new LmaxSandboxMarketDataSummary(
            options.RunKey,
            started,
            ended,
            options.DurationSeconds,
            source,
            options.Instruments,
            events.Count,
            events.Count(x => x.Classification == "Snapshot"),
            events.Count(x => x.Classification == "Incremental"),
            rejectCount,
            timeoutCount,
            malformedCount,
            events.Count(x => x.Classification == "Unknown"),
            logonCount,
            sessionRejectCount,
            marketDataRequestRejectCount,
            logoutCount,
            credentialsRejectedCount,
            firstReject?.RefTagId,
            firstReject?.RefMsgType,
            primaryFailureReason,
            finalFixSessionState,
            timeoutAfterPrimaryFailureCount,
            terminalTimeoutCount,
            boundedCaptureEndedCleanly,
            events.Count == 0 ? null : events.Min(x => x.ReceivedAtUtc),
            events.Count == 0 ? null : events.Max(x => x.ReceivedAtUtc),
            status);
    }

    private static string FixMessage(params string[] fields)
    {
        var body = string.Join('\u0001', fields) + '\u0001';
        var header = $"8=FIX.4.4\u00019={Encoding.ASCII.GetByteCount(body)}\u0001";
        var withoutChecksum = header + body;
        var checksum = Encoding.ASCII.GetBytes(withoutChecksum).Sum(x => x) % 256;
        return withoutChecksum + $"10={checksum:000}\u0001";
    }

    private static string SanitizeFailure(string message)
        => message
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "554=[redacted]", StringComparison.OrdinalIgnoreCase);

    private static string EffectiveSenderCompId(LmaxSandboxMarketDataOptions options)
    {
        if (options.NoExternal)
        {
            return options.SenderCompId;
        }

        var username = Environment.GetEnvironmentVariable(options.UsernameSecretRef ?? string.Empty);
        return string.IsNullOrWhiteSpace(username) ? options.SenderCompId : username;
    }

    private static int IndexOfTag(IReadOnlyList<string> tagOrder, string tag)
    {
        for (var i = 0; i < tagOrder.Count; i++)
        {
            if (string.Equals(tagOrder[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TagOccursBetween(IReadOnlyList<string> tagOrder, string tag, int startExclusive, int endExclusive)
    {
        for (var i = startExclusive + 1; i < endExclusive; i++)
        {
            if (string.Equals(tagOrder[i], tag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ShapeOfRequestId(string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return requestId.Length <= 96 && requestId.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            ? "run-key-compatible"
            : "nonstandard";
    }
}

public sealed record LmaxSandboxCollectorDesignReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string LmaxSandboxCollectorCoded,
    string Scope,
    IReadOnlyList<string> Components,
    IReadOnlyList<string> ExplicitlyOutOfScope);

public sealed record LmaxSandboxCaptureReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string LmaxSandboxCollectorCoded,
    string LmaxSandboxPreflightGate,
    string LmaxSandboxFakeServerTestsPass,
    string LmaxExternalCallsAttempted,
    string LmaxSandboxExternalCaptureAttempted,
    string LmaxSandboxExternalCaptureStatus,
    string LmaxOrderPathDisabled,
    string LmaxTradingEndpointBlocked,
    string LmaxDbPersistenceEnabled,
    string LmaxContinuousUnboundedFeed,
    string LmaxBoundedSandboxReady,
    string FirstStepFetchLiveMarketDataStatus,
    string SafeNextPhase,
    LmaxSandboxMarketDataSummary Summary,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings);

public sealed record LmaxSandboxRiskBoundaryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string LmaxOrderPathDisabled,
    string LmaxTradingEndpointBlocked,
    string LmaxDbPersistenceEnabled,
    string LmaxContinuousUnboundedFeed,
    IReadOnlyList<string> ForbiddenActionsNotPerformed,
    IReadOnlyList<string> BoundaryNotes);

public sealed record LmaxSandboxRunClassificationReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    int SessionRejectCount,
    int MarketDataRequestRejectCount,
    int LogonCount,
    int UnknownCount,
    int LogoutCount,
    int CredentialsRejectedCount,
    string? RefTagId,
    string? RefMsgType,
    string PrimaryFailureReason,
    string FinalFixSessionState,
    int TimeoutAfterPrimaryFailureCount,
    int TerminalTimeoutCount,
    bool BoundedCaptureEndedCleanly,
    string FirstStepFetchLiveMarketDataStatus,
    string SafeNextPhase);

public sealed record LmaxSandboxSuccessClassificationReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string FirstStepFetchLiveMarketDataStatus,
    string LmaxSandboxExternalCaptureStatus,
    string PrimaryFailureReason,
    string MarketDataObserved,
    int SnapshotsObserved,
    int IncrementalsObserved,
    string LogonObserved,
    int LogonCount,
    int UnknownCount,
    int SessionRejects,
    int Logouts,
    int CredentialsRejected,
    int TerminalTimeoutCount,
    string BoundedCaptureEndedCleanly,
    string NoCredentialValuesPersisted,
    string NoProductionExecutionPath,
    string MarketDataLmaxDbStatus);

public sealed record LmaxMarketDataRequestGroupDiagnosisReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string OutgoingRedactedFix,
    IReadOnlyList<string> OutgoingTagOrder,
    string? OutgoingTag262ValueShape,
    string? OutgoingTag263Value,
    string? OutgoingTag264Value,
    string? OutgoingTag265Value,
    string? OutgoingTag146Value,
    string OutgoingTag48Present,
    string? OutgoingTag48Value,
    string? OutgoingTag22Value,
    string? OutgoingTag267Value,
    IReadOnlyList<string> OutgoingTag269Values,
    int ComputedInstrumentGroupsAfter146,
    int ComputedMdEntryTypeGroupsAfter267,
    string? PrimaryRejectTag,
    string? PrimaryRejectMessageType,
    string PrimaryFailureReason,
    string DidOutgoingRequestContainExactlyOneValidInstrumentGroupAfter146);

public sealed record LmaxSandboxLogonAuditReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string FixLogonTag49Source,
    string FixLogonTag56Value,
    string FixLogonTag553Source,
    string FixLogonTag554Source,
    string FixLogonPasswordRedacted,
    string FixLogonTargetIsMarketData,
    string FixLogonTradingEndpointBlocked);

public sealed class LmaxSandboxMarketDataArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(LmaxSandboxMarketDataOptions options, LmaxSandboxMarketDataRunResult result, CancellationToken cancellationToken)
    {
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var marketDataRoot = Path.Combine(outputRoot, "marketdata");
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(marketDataRoot);
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        await WriteEventsAsync(Path.Combine(marketDataRoot, "lmax_marketdata_events.jsonl"), result.Events, cancellationToken);
        await WriteJsonAsync(Path.Combine(marketDataRoot, "lmax_marketdata_summary.json"), result.Summary, cancellationToken);

        var design = new LmaxSandboxCollectorDesignReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            "YES",
            "LMAX Demo/Sandbox market-data read-only bounded collector; no trading, no Qubes, no PMS/OMS/EMS.",
            [
                "Strict preflight gate",
                "FakeFixTransport for no-external validation",
                "TlsTcpFixTransport for future operator-approved sandbox capture",
                "FIX message policy allow-list",
                "Bounded read lifecycle",
                "Local artifact-only output"
            ],
            [
                "Qubes weights",
                "drifts/orders/fills/routes/broker/live-state",
                "PMS/OMS/EMS",
                "DB persistence",
                "scheduler or permanent service",
                "LMAX production or order endpoint"
            ]);
        var capture = BuildCaptureReport(options, result);
        var classificationReport = new LmaxSandboxRunClassificationReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            result.Summary.SessionRejectCount,
            result.Summary.MarketDataRequestRejectCount,
            result.Summary.LogonCount,
            result.Summary.Unknown,
            result.Summary.LogoutCount,
            result.Summary.CredentialsRejectedCount,
            result.Summary.RefTagId,
            result.Summary.RefMsgType,
            result.Summary.PrimaryFailureReason,
            result.Summary.FinalFixSessionState,
            result.Summary.TimeoutAfterPrimaryFailureCount,
            result.Summary.TerminalTimeoutCount,
            result.Summary.BoundedCaptureEndedCleanly,
            capture.FirstStepFetchLiveMarketDataStatus,
            capture.SafeNextPhase);
        var successClassification = new LmaxSandboxSuccessClassificationReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            capture.FirstStepFetchLiveMarketDataStatus,
            capture.LmaxSandboxExternalCaptureStatus,
            result.Summary.PrimaryFailureReason,
            result.Summary.Snapshots > 0 || result.Summary.Incrementals > 0 ? "YES" : "NO",
            result.Summary.Snapshots,
            result.Summary.Incrementals,
            result.Summary.LogonCount > 0 ? "YES" : "NO",
            result.Summary.LogonCount,
            result.Summary.Unknown,
            result.Summary.SessionRejectCount,
            result.Summary.LogoutCount,
            result.Summary.CredentialsRejectedCount,
            result.Summary.TerminalTimeoutCount,
            result.Summary.BoundedCaptureEndedCleanly ? "YES" : "NO",
            "YES",
            result.Preflight.TradingEndpointBlocked && options.DisableOrderPath && !result.Failures.Any(x => x.Contains("production", StringComparison.OrdinalIgnoreCase)) ? "PASS" : "FAIL",
            "AdoptedWithWarnings");
        var logonAudit = new LmaxSandboxLogonAuditReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            options.NoExternal ? "SenderCompIdOption" : "LMAX_DEMO_MD_USERNAME",
            options.TargetCompId,
            "LMAX_DEMO_MD_USERNAME",
            "LMAX_DEMO_MD_PASSWORD",
            "YES",
            string.Equals(options.TargetCompId, LmaxSandboxMarketDataOptions.DemoMarketDataTargetCompId, StringComparison.Ordinal) ? "YES" : "NO",
            result.Preflight.TradingEndpointBlocked ? "YES" : "NO");
        var requestGroupDiagnosis = BuildRequestGroupDiagnosisReport(options, result);
        var fake = new
        {
            run_key = options.RunKey,
            created_at_utc = DateTimeOffset.UtcNow,
            use_fake_fix_server = options.UseFakeFixServer,
            no_external = options.NoExternal,
            messages_read = result.Summary.MessagesRead,
            snapshots = result.Summary.Snapshots,
            incrementals = result.Summary.Incrementals,
            rejects = result.Summary.Rejects,
            LMAX_SANDBOX_FAKE_SERVER_TESTS_PASS = !options.UseFakeFixServer
                ? "NOT_APPLICABLE"
                : result.Summary.MessagesRead > 0 && !result.ExternalCallsAttempted ? "YES" : "NO"
        };
        var risk = new LmaxSandboxRiskBoundaryReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            "YES",
            result.Preflight.TradingEndpointBlocked ? "YES" : "NO",
            "NO",
            "NO",
            [
                "No Qubes, weights, drifts, orders, fills, routes, broker/live-state, PMS, OMS, EMS, manager, or Anubis was called.",
                "No MarketDataSnapshots, MarketDataBars, LmaxIndividualTrades, or LmaxTradeSummaries write path exists in this collector.",
                "No A.txt, H.txt, or I.txt is created.",
                "No external calls are attempted during no-external fake validation."
            ],
            [
                "LMAX order path exists elsewhere in the repository but this collector allows only 35=A/0/1/5/V and blocks 35=D/F/G.",
                "Future sandbox run requires exact fresh operator approval phrase and remains duration/message bounded."
            ]);

        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_collector_design", design, Markdown.Design(design), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_preflight_report", result.Preflight, Markdown.Preflight(result.Preflight), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_capture_report", capture, Markdown.Capture(capture), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_run_classification", classificationReport, Markdown.RunClassification(classificationReport), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_success_classification", successClassification, Markdown.SuccessClassification(successClassification), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_logon_audit", logonAudit, Markdown.LogonAudit(logonAudit), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_marketdata_request_group_diagnosis", requestGroupDiagnosis, Markdown.RequestGroupDiagnosis(requestGroupDiagnosis), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_fake_server_test_report", fake, Markdown.FakeServer(fake), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "lmax_sandbox_marketdata_risk_boundary_report", risk, Markdown.Risk(risk), cancellationToken);
        var shareMarkdown = Markdown.Share(capture);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_sandbox_marketdata_setup_summary.md"), shareMarkdown, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_sandbox_marketdata_capture_summary.md"), shareMarkdown, cancellationToken);

        await WriteManifestAsync(outputRoot, options.RunKey, options.NoExternal, cancellationToken);
    }

    private static LmaxMarketDataRequestGroupDiagnosisReport BuildRequestGroupDiagnosisReport(LmaxSandboxMarketDataOptions options, LmaxSandboxMarketDataRunResult result)
    {
        if (result.Preflight.InstrumentMappings.Count == 1 &&
            !string.IsNullOrWhiteSpace(result.Preflight.InstrumentMappings[0].SecurityId) &&
            !string.IsNullOrWhiteSpace(result.Preflight.InstrumentMappings[0].SecurityIdSource))
        {
            return LmaxSandboxMarketDataCollector.DiagnoseMarketDataRequestGroup(options, result.Preflight.InstrumentMappings, result.Events);
        }

        return new LmaxMarketDataRequestGroupDiagnosisReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            string.Empty,
            [],
            null,
            null,
            null,
            null,
            null,
            "NO",
            null,
            null,
            null,
            [],
            0,
            0,
            result.Summary.RefTagId,
            result.Summary.RefMsgType,
            result.Summary.PrimaryFailureReason,
            "UNKNOWN");
    }

    private static LmaxSandboxCaptureReport BuildCaptureReport(LmaxSandboxMarketDataOptions options, LmaxSandboxMarketDataRunResult result)
    {
        var fakePass = options.UseFakeFixServer && options.NoExternal && !result.ExternalCallsAttempted && result.Summary.MessagesRead > 0 && result.Failures.Count == 0;
        var hasMarketData = result.Summary.Snapshots > 0 || result.Summary.Incrementals > 0;
        var firstStepStatus = "UNKNOWN";
        if (!result.Preflight.CredentialsAvailable)
        {
            firstStepStatus = "FAILED";
        }
        else if (result.Failures.Count > 0 || result.Preflight.LmaxSandboxPreflightGate == "FAIL")
        {
            firstStepStatus = "FAILED";
        }
        else if (options.NoExternal)
        {
            firstStepStatus = "CODED_FAKE_ONLY";
        }
        else if (hasMarketData)
        {
            firstStepStatus = "SANDBOX_CAPTURED";
        }
        else if (result.Summary.CredentialsRejectedCount > 0)
        {
            firstStepStatus = "SANDBOX_CREDENTIALS_REJECTED";
        }
        else if (result.Summary.Rejects > 0)
        {
            firstStepStatus = "SANDBOX_REJECT_CLASSIFIED";
        }
        else if (result.Summary.Timeouts > 0)
        {
            firstStepStatus = "SANDBOX_TIMEOUT_CLASSIFIED";
        }

        var safeNextPhase = firstStepStatus switch
        {
            "CODED_FAKE_ONLY" => "RUN_OPERATOR_APPROVED_SANDBOX_CAPTURE",
            "SANDBOX_CAPTURED" => "BUILD_BOUNDED_COLLECTOR_LOOP",
            "SANDBOX_CREDENTIALS_REJECTED" => "FIX_DEMO_MARKETDATA_CREDENTIALS_AND_RETRY",
            "SANDBOX_REJECT_CLASSIFIED" => "STATUS_ONLY",
            "SANDBOX_TIMEOUT_CLASSIFIED" => "STATUS_ONLY",
            _ => "BLOCKED"
        };
        var boundedReady = fakePass && result.Preflight.LmaxSandboxPreflightGate == "PASS" ? "YES" : "NO";
        return new LmaxSandboxCaptureReport(
            options.RunKey,
            DateTimeOffset.UtcNow,
            "YES",
            result.Preflight.LmaxSandboxPreflightGate,
            !options.UseFakeFixServer ? "NOT_APPLICABLE" : fakePass ? "YES" : "NO",
            result.ExternalCallsAttempted ? "YES" : "NO",
            result.ExternalCallsAttempted ? "YES" : "NO",
            options.NoExternal || !result.ExternalCallsAttempted ? "NOT_ATTEMPTED" : result.Summary.Status,
            options.DisableOrderPath ? "YES" : "NO",
            result.Preflight.TradingEndpointBlocked ? "YES" : "NO",
            "NO",
            "NO",
            boundedReady,
            firstStepStatus,
            safeNextPhase,
            result.Summary,
            result.Failures,
            result.Warnings);
    }

    private static async Task WriteEventsAsync(string path, IReadOnlyList<LmaxSandboxMarketDataEvent> events, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        foreach (var item in events)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(item, JsonLineOptions).AsMemory(), cancellationToken);
        }
    }

    private static async Task WriteJsonAndMarkdown<T>(string root, string basename, T report, string markdown, CancellationToken cancellationToken)
    {
        await WriteJsonAsync(Path.Combine(root, $"{basename}.json"), report, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
        => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    private static async Task WriteManifestAsync(string outputRoot, string runKey, bool noExternal, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "hashes.json", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(Path.GetFileName(path), "manifest.sha256", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = await Sha256Async(file, cancellationToken);
        }

        var manifest = new
        {
            run_key = runKey,
            created_at_utc = DateTimeOffset.UtcNow,
            package_type = "lmax_sandbox_marketdata_collector",
            no_external = noExternal,
            db_persistence_enabled = false,
            files = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await WriteJsonAsync(manifestPath, manifest, cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputRoot, "hashes.json"), hashes, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{hashes["manifest.json"]}  manifest.json{Environment.NewLine}", cancellationToken);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static class Markdown
    {
        public static string Design(LmaxSandboxCollectorDesignReport report)
            => Lines(
                "# LMAX Sandbox Market Data Collector Design",
                "",
                $"- LMAX_SANDBOX_COLLECTOR_CODED: `{report.LmaxSandboxCollectorCoded}`",
                $"- Scope: {report.Scope}",
                "",
                "## Components",
                Bullets(report.Components),
                "## Explicitly out of scope",
                Bullets(report.ExplicitlyOutOfScope));

        public static string Preflight(LmaxSandboxMarketDataPreflightReport report)
            => Lines(
                "# LMAX Sandbox Market Data Preflight",
                "",
                $"- LMAX_SANDBOX_PREFLIGHT_GATE: `{report.LmaxSandboxPreflightGate}`",
                $"- environment_demo: `{report.EnvironmentDemo}`",
                $"- market_data_endpoint_only: `{report.MarketDataEndpointOnly}`",
                $"- trading_endpoint_blocked: `{report.TradingEndpointBlocked}`",
                $"- target_comp_id_market_data_only: `{report.TargetCompIdMarketDataOnly}`",
                $"- no_execution: `{report.NoExecution}`",
                $"- order_path_disabled: `{report.OrderPathDisabled}`",
                $"- duration_bounded: `{report.DurationBounded}`",
                $"- max_messages_bounded: `{report.MaxMessagesBounded}`",
                $"- credentials_available: `{report.CredentialsAvailable}`",
                $"- md_update_type: `{report.MdUpdateType}`",
                $"- md_update_type_valid: `{report.MdUpdateTypeValid}`",
                $"- md_update_type_gate: `{report.MdUpdateTypeGate}`",
                "",
                "## Instruments",
                Bullets(report.InstrumentMappings.Select(x => $"{x.Instrument}: SecurityID={x.SecurityId}, SecurityIDSource={x.SecurityIdSource}, confidence={x.MappingConfidence}")),
                "## Failures",
                Bullets(report.Failures),
                "## Warnings",
                Bullets(report.Warnings));

        public static string Capture(LmaxSandboxCaptureReport report)
            => Lines(
                "# LMAX Sandbox Market Data Capture Report",
                "",
                $"- LMAX_SANDBOX_COLLECTOR_CODED: `{report.LmaxSandboxCollectorCoded}`",
                $"- LMAX_SANDBOX_PREFLIGHT_GATE: `{report.LmaxSandboxPreflightGate}`",
                $"- LMAX_SANDBOX_FAKE_SERVER_TESTS_PASS: `{report.LmaxSandboxFakeServerTestsPass}`",
                $"- LMAX_EXTERNAL_CALLS_ATTEMPTED: `{report.LmaxExternalCallsAttempted}`",
                $"- LMAX_SANDBOX_EXTERNAL_CAPTURE_ATTEMPTED: `{report.LmaxSandboxExternalCaptureAttempted}`",
                $"- LMAX_SANDBOX_EXTERNAL_CAPTURE_STATUS: `{report.LmaxSandboxExternalCaptureStatus}`",
                $"- LMAX_ORDER_PATH_DISABLED: `{report.LmaxOrderPathDisabled}`",
                $"- LMAX_TRADING_ENDPOINT_BLOCKED: `{report.LmaxTradingEndpointBlocked}`",
                $"- LMAX_DB_PERSISTENCE_ENABLED: `{report.LmaxDbPersistenceEnabled}`",
                $"- LMAX_CONTINUOUS_UNBOUNDED_FEED: `{report.LmaxContinuousUnboundedFeed}`",
                $"- LMAX_BOUNDED_SANDBOX_READY: `{report.LmaxBoundedSandboxReady}`",
                $"- FIRST_STEP_FETCH_LIVE_MARKET_DATA_STATUS: `{report.FirstStepFetchLiveMarketDataStatus}`",
                $"- SAFE_NEXT_PHASE: `{report.SafeNextPhase}`",
                "",
                "## Summary",
                $"- messages_read: `{report.Summary.MessagesRead}`",
                $"- snapshots: `{report.Summary.Snapshots}`",
                $"- incrementals: `{report.Summary.Incrementals}`",
                $"- rejects: `{report.Summary.Rejects}`",
                $"- logons: `{report.Summary.LogonCount}`",
                $"- unknown: `{report.Summary.Unknown}`",
                $"- session_rejects: `{report.Summary.SessionRejectCount}`",
                $"- market_data_request_rejects: `{report.Summary.MarketDataRequestRejectCount}`",
                $"- ref_tag_id: `{report.Summary.RefTagId}`",
                $"- ref_msg_type: `{report.Summary.RefMsgType}`",
                $"- logouts: `{report.Summary.LogoutCount}`",
                $"- credentials_rejected: `{report.Summary.CredentialsRejectedCount}`",
                $"- primary_failure_reason: `{report.Summary.PrimaryFailureReason}`",
                $"- final_fix_session_state: `{report.Summary.FinalFixSessionState}`",
                $"- timeout_after_primary_failure: `{report.Summary.TimeoutAfterPrimaryFailureCount}`",
                $"- terminal_timeout_count: `{report.Summary.TerminalTimeoutCount}`",
                $"- bounded_capture_ended_cleanly: `{report.Summary.BoundedCaptureEndedCleanly}`",
                $"- status: `{report.Summary.Status}`",
                "",
                "## Failures",
                Bullets(report.Failures),
                "## Warnings",
                Bullets(report.Warnings));

        public static string FakeServer(object report)
            => Lines("# LMAX Sandbox Market Data Fake Server Test Report", "", "Fake no-external collector run completed. See JSON for counters.");

        public static string RunClassification(LmaxSandboxRunClassificationReport report)
            => Lines(
                "# LMAX Sandbox Market Data Run Classification",
                "",
                $"- sessionRejectCount: `{report.SessionRejectCount}`",
                $"- marketDataRequestRejectCount: `{report.MarketDataRequestRejectCount}`",
                $"- logonCount: `{report.LogonCount}`",
                $"- unknownCount: `{report.UnknownCount}`",
                $"- logoutCount: `{report.LogoutCount}`",
                $"- credentialsRejectedCount: `{report.CredentialsRejectedCount}`",
                $"- refTagId: `{report.RefTagId}`",
                $"- refMsgType: `{report.RefMsgType}`",
                $"- primaryFailureReason: `{report.PrimaryFailureReason}`",
                $"- finalFixSessionState: `{report.FinalFixSessionState}`",
                $"- timeoutAfterPrimaryFailureCount: `{report.TimeoutAfterPrimaryFailureCount}`",
                $"- terminalTimeoutCount: `{report.TerminalTimeoutCount}`",
                $"- boundedCaptureEndedCleanly: `{report.BoundedCaptureEndedCleanly}`",
                $"- FIRST_STEP_FETCH_LIVE_MARKET_DATA_STATUS: `{report.FirstStepFetchLiveMarketDataStatus}`",
                $"- SAFE_NEXT_PHASE: `{report.SafeNextPhase}`");

        public static string SuccessClassification(LmaxSandboxSuccessClassificationReport report)
            => Lines(
                "# LMAX Sandbox Market Data Success Classification",
                "",
                $"- FIRST_STEP_FETCH_LIVE_MARKET_DATA_STATUS = `{report.FirstStepFetchLiveMarketDataStatus}`",
                $"- LMAX_SANDBOX_EXTERNAL_CAPTURE_STATUS = `{report.LmaxSandboxExternalCaptureStatus}`",
                $"- PRIMARY_FAILURE_REASON = `{report.PrimaryFailureReason}`",
                $"- MARKETDATA_OBSERVED = `{report.MarketDataObserved}`",
                $"- SNAPSHOTS_OBSERVED = `{report.SnapshotsObserved}`",
                $"- INCREMENTALS_OBSERVED = `{report.IncrementalsObserved}`",
                $"- LOGON_OBSERVED = `{report.LogonObserved}`",
                $"- LOGON_COUNT = `{report.LogonCount}`",
                $"- UNKNOWN_COUNT = `{report.UnknownCount}`",
                $"- SESSION_REJECTS = `{report.SessionRejects}`",
                $"- LOGOUTS = `{report.Logouts}`",
                $"- CREDENTIALS_REJECTED = `{report.CredentialsRejected}`",
                $"- TERMINAL_TIMEOUT_COUNT = `{report.TerminalTimeoutCount}`",
                $"- BOUNDED_CAPTURE_ENDED_CLEANLY = `{report.BoundedCaptureEndedCleanly}`",
                $"- NO_CREDENTIAL_VALUES_PERSISTED = `{report.NoCredentialValuesPersisted}`",
                $"- NO_PRODUCTION_EXECUTION_PATH = `{report.NoProductionExecutionPath}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report.MarketDataLmaxDbStatus}`");

        public static string LogonAudit(LmaxSandboxLogonAuditReport report)
            => Lines(
                "# LMAX Sandbox Market Data Logon Audit",
                "",
                $"- FIX_LOGON_TAG_49_SOURCE = `{report.FixLogonTag49Source}`",
                $"- FIX_LOGON_TAG_56_VALUE = `{report.FixLogonTag56Value}`",
                $"- FIX_LOGON_TAG_553_SOURCE = `{report.FixLogonTag553Source}`",
                $"- FIX_LOGON_TAG_554_SOURCE = `{report.FixLogonTag554Source}`",
                $"- FIX_LOGON_PASSWORD_REDACTED = `{report.FixLogonPasswordRedacted}`",
                $"- FIX_LOGON_TARGET_IS_MARKETDATA = `{report.FixLogonTargetIsMarketData}`",
                $"- FIX_LOGON_TRADING_ENDPOINT_BLOCKED = `{report.FixLogonTradingEndpointBlocked}`");

        public static string RequestGroupDiagnosis(LmaxMarketDataRequestGroupDiagnosisReport report)
            => Lines(
                "# LMAX MarketDataRequest Group Diagnosis",
                "",
                $"- outgoing_tag_order: `{string.Join(",", report.OutgoingTagOrder)}`",
                $"- outgoing_tag_262_value_shape: `{report.OutgoingTag262ValueShape}`",
                $"- outgoing_tag_263_value: `{report.OutgoingTag263Value}`",
                $"- outgoing_tag_264_value: `{report.OutgoingTag264Value}`",
                $"- outgoing_tag_265_value: `{report.OutgoingTag265Value}`",
                $"- outgoing_tag_146_value: `{report.OutgoingTag146Value}`",
                $"- outgoing_tag_48_present: `{report.OutgoingTag48Present}`",
                $"- outgoing_tag_48_value: `{report.OutgoingTag48Value}`",
                $"- outgoing_tag_22_value: `{report.OutgoingTag22Value}`",
                $"- outgoing_tag_267_value: `{report.OutgoingTag267Value}`",
                $"- outgoing_tag_269_values: `{string.Join(",", report.OutgoingTag269Values)}`",
                $"- computed_number_of_instrument_groups_after_146: `{report.ComputedInstrumentGroupsAfter146}`",
                $"- computed_number_of_MDEntryType_groups_after_267: `{report.ComputedMdEntryTypeGroupsAfter267}`",
                $"- primary_reject_tag: `{report.PrimaryRejectTag}`",
                $"- primary_reject_message_type: `{report.PrimaryRejectMessageType}`",
                $"- primary_failure_reason: `{report.PrimaryFailureReason}`",
                $"- DID_OUTGOING_REQUEST_CONTAIN_EXACTLY_ONE_VALID_INSTRUMENT_GROUP_AFTER_146: `{report.DidOutgoingRequestContainExactlyOneValidInstrumentGroupAfter146}`",
                "",
                "## Redacted outgoing FIX",
                $"`{report.OutgoingRedactedFix}`");

        public static string Risk(LmaxSandboxRiskBoundaryReport report)
            => Lines(
                "# LMAX Sandbox Market Data Risk Boundary",
                "",
                $"- LMAX_ORDER_PATH_DISABLED: `{report.LmaxOrderPathDisabled}`",
                $"- LMAX_TRADING_ENDPOINT_BLOCKED: `{report.LmaxTradingEndpointBlocked}`",
                $"- LMAX_DB_PERSISTENCE_ENABLED: `{report.LmaxDbPersistenceEnabled}`",
                $"- LMAX_CONTINUOUS_UNBOUNDED_FEED: `{report.LmaxContinuousUnboundedFeed}`",
                "",
                "## Forbidden actions not performed",
                Bullets(report.ForbiddenActionsNotPerformed),
                "## Boundary notes",
                Bullets(report.BoundaryNotes));

        public static string Share(LmaxSandboxCaptureReport report)
            => Lines(
                "# LMAX Sandbox Market Data Setup Summary",
                "",
                $"- First step fetch live market data: `{report.FirstStepFetchLiveMarketDataStatus}`.",
                $"- run_key: `{report.RunKey}`.",
                $"- started_at_utc: `{report.Summary.StartedAtUtc:O}`.",
                $"- ended_at_utc: `{report.Summary.EndedAtUtc:O}`.",
                $"- duration_seconds: `{report.Summary.DurationSeconds}`.",
                $"- instrument: `{string.Join(",", report.Summary.Instruments)}`.",
                $"- source: `{report.Summary.Source}`.",
                $"- messages_read: `{report.Summary.MessagesRead}`.",
                $"- snapshots: `{report.Summary.Snapshots}`.",
                $"- incrementals: `{report.Summary.Incrementals}`.",
                $"- rejects: `{report.Summary.Rejects}`.",
                $"- sessionRejectCount: `{report.Summary.SessionRejectCount}`.",
                $"- marketDataRequestRejectCount: `{report.Summary.MarketDataRequestRejectCount}`.",
                $"- timeouts: `{report.Summary.Timeouts}`.",
                $"- malformed: `{report.Summary.Malformed}`.",
                $"- unknown: `{report.Summary.Unknown}`.",
                $"- logonCount: `{report.Summary.LogonCount}`.",
                $"- logoutCount: `{report.Summary.LogoutCount}`.",
                $"- credentialsRejectedCount: `{report.Summary.CredentialsRejectedCount}`.",
                $"- primaryFailureReason: `{report.Summary.PrimaryFailureReason}`.",
                $"- timeoutAfterPrimaryFailureCount: `{report.Summary.TimeoutAfterPrimaryFailureCount}`.",
                $"- terminalTimeoutCount: `{report.Summary.TerminalTimeoutCount}`.",
                $"- boundedCaptureEndedCleanly: `{report.Summary.BoundedCaptureEndedCleanly}`.",
                $"- finalFixSessionState: `{report.Summary.FinalFixSessionState}`.",
                $"- first_received_at_utc: `{report.Summary.FirstReceivedAtUtc:O}`.",
                $"- last_received_at_utc: `{report.Summary.LastReceivedAtUtc:O}`.",
                $"- capture_status: `{report.LmaxSandboxExternalCaptureStatus}`.",
                $"- boundary_status: `{(report.LmaxOrderPathDisabled == "YES" && report.LmaxTradingEndpointBlocked == "YES" && report.LmaxDbPersistenceEnabled == "NO" ? "PASS" : "FAIL")}`.",
                $"- Fake no-external validation: `{report.LmaxSandboxFakeServerTestsPass}`.",
                $"- External calls attempted: `{report.LmaxExternalCallsAttempted}`.",
                $"- DB persistence enabled: `{report.LmaxDbPersistenceEnabled}`.",
                $"- Continuous unbounded feed: `{report.LmaxContinuousUnboundedFeed}`.",
                $"- Safe next phase: `{report.SafeNextPhase}`.",
                "",
                "Operator-approved sandbox command is documented in the task request and must not be run without fresh explicit approval.");

        private static string Bullets(IEnumerable<string> values)
        {
            var lines = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return lines.Length == 0 ? "- none" : string.Join(Environment.NewLine, lines.Select(x => $"- `{x}`"));
        }

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
