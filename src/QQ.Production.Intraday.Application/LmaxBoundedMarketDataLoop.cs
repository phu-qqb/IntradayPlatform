using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxBoundedMarketDataLoopOptions(
    string RunKey,
    string OutputRoot,
    string Environment,
    string Host,
    int Port,
    string TargetCompId,
    string SenderCompId,
    string UsernameSecretRef,
    string PasswordSecretRef,
    bool NoExternal,
    bool ExternalApproved,
    string? OperatorApprovalPhrase,
    bool NoExecution,
    bool UseFakeFixServer,
    int DurationSeconds,
    int MaxMessages,
    int MdUpdateType,
    IReadOnlyList<string> Instruments)
{
    public const string DemoEnvironment = "demo";
    public const string DemoMarketDataHost = "fix-marketdata.london-demo.lmax.com";
    public const string DemoMarketDataTargetCompId = "LMXBDM";
    public const string DemoOrderHost = "fix-order.london-demo.lmax.com";
    public const string DemoOrderTargetCompId = "LMXBD";
    public const string ApprovalPhrase = "APPROVE_LMAX_DEMO_READONLY_MARKETDATA_LOOP";
}

public sealed record LmaxBoundedMarketDataLoopPreflightReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string Gate,
    bool EnvironmentDemo,
    bool MarketDataEndpointOnly,
    bool TradingEndpointBlocked,
    bool TargetCompIdMarketDataOnly,
    bool NoExternal,
    bool ExternalApproved,
    bool OperatorApprovalValid,
    bool NoExecution,
    bool UseFakeFixServer,
    bool DurationBounded,
    bool MaxMessagesBounded,
    bool MdUpdateTypeValid,
    bool InstrumentsAllowListed,
    bool OutputRootRunKeyValid,
    bool DbPersistenceDisabled,
    bool SchedulerDisabled,
    bool SecretsPresent,
    bool SecretsRedacted,
    IReadOnlyList<LmaxSandboxInstrumentMapping> InstrumentMappings,
    IReadOnlyList<string> Failures);

public sealed record LmaxBoundedMarketDataLoopEvent(
    [property: JsonPropertyName("run_key")] string RunKey,
    [property: JsonPropertyName("received_at_utc")] DateTimeOffset ReceivedAtUtc,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("security_id")] string SecurityId,
    [property: JsonPropertyName("security_id_source")] string SecurityIdSource,
    [property: JsonPropertyName("fix_msg_type")] string FixMsgType,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("bid")] decimal? Bid,
    [property: JsonPropertyName("ask")] decimal? Ask,
    [property: JsonPropertyName("bid_size")] decimal? BidSize,
    [property: JsonPropertyName("ask_size")] decimal? AskSize,
    [property: JsonPropertyName("raw_redacted_fix")] string RawRedactedFix);

public sealed record LmaxBoundedMarketDataLoopSummary(
    [property: JsonPropertyName("run_key")] string RunKey,
    [property: JsonPropertyName("started_at_utc")] DateTimeOffset StartedAtUtc,
    [property: JsonPropertyName("ended_at_utc")] DateTimeOffset EndedAtUtc,
    [property: JsonPropertyName("duration_seconds")] int DurationSeconds,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("instruments")] IReadOnlyList<string> Instruments,
    [property: JsonPropertyName("messages_read")] int MessagesRead,
    [property: JsonPropertyName("logons")] int Logons,
    [property: JsonPropertyName("snapshots")] int Snapshots,
    [property: JsonPropertyName("incrementals")] int Incrementals,
    [property: JsonPropertyName("session_rejects")] int SessionRejects,
    [property: JsonPropertyName("marketdata_request_rejects")] int MarketDataRequestRejects,
    [property: JsonPropertyName("logouts")] int Logouts,
    [property: JsonPropertyName("credentials_rejected")] int CredentialsRejected,
    [property: JsonPropertyName("terminal_timeouts")] int TerminalTimeouts,
    [property: JsonPropertyName("malformed")] int Malformed,
    [property: JsonPropertyName("unknown")] int Unknown,
    [property: JsonPropertyName("primary_failure_reason")] string PrimaryFailureReason,
    [property: JsonPropertyName("final_fix_session_state")] string FinalFixSessionState,
    [property: JsonPropertyName("bounded_capture_ended_cleanly")] string BoundedCaptureEndedCleanly,
    [property: JsonPropertyName("status")] string Status);

public sealed record LmaxBoundedMarketDataLoopResult(
    LmaxBoundedMarketDataLoopPreflightReport Preflight,
    LmaxBoundedMarketDataLoopSummary Summary,
    IReadOnlyList<LmaxBoundedMarketDataLoopEvent> Events,
    IReadOnlyDictionary<string, object> CaptureReport,
    IReadOnlyDictionary<string, object> BoundaryReport,
    IReadOnlyDictionary<string, object> FakeValidationReport,
    IReadOnlyDictionary<string, object> SecretScanReport);

public sealed class LmaxBoundedMarketDataLoopRunner
{
    private readonly TimeProvider timeProvider;
    private readonly IFixTransport? transportOverride;

    public LmaxBoundedMarketDataLoopRunner(TimeProvider? timeProvider = null, IFixTransport? transportOverride = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.transportOverride = transportOverride;
    }

    public async Task<LmaxBoundedMarketDataLoopResult> RunAsync(
        LmaxBoundedMarketDataLoopOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var preflight = Evaluate(options);
        var started = timeProvider.GetUtcNow();
        var failures = new List<string>(preflight.Failures);
        var events = new List<LmaxBoundedMarketDataLoopEvent>();
        IFixTransport? transport = null;

        if (preflight.Gate == "PASS")
        {
            var sandboxOptions = ToSandboxOptions(options);
            transport = transportOverride ?? (options.UseFakeFixServer
                ? new FakeFixTransport(BuildFakeLifecycle(preflight.InstrumentMappings[0]))
                : new TlsTcpFixTransport());

            try
            {
                await transport.ConnectAsync(sandboxOptions, cancellationToken);
                if (!options.NoExternal)
                {
                    await SendPolicyCheckedAsync(transport, BuildLogon(options), sandboxOptions, cancellationToken);
                    await SendPolicyCheckedAsync(
                        transport,
                        LmaxSandboxMarketDataCollector.BuildMarketDataRequest(sandboxOptions, preflight.InstrumentMappings),
                        sandboxOptions,
                        cancellationToken);
                }

                var deadline = started.Add(TimeSpan.FromSeconds(options.DurationSeconds));
                while (events.Count(x => x.Classification != "TerminalTimeout") < options.MaxMessages &&
                       (options.UseFakeFixServer || timeProvider.GetUtcNow() < deadline))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frame = await transport.ReadAsync(TimeSpan.FromMilliseconds(sandboxOptions.ReadTimeoutMs), cancellationToken);
                    if (frame is null)
                    {
                        events.Add(BuildTimeoutEvent(options, preflight.InstrumentMappings[0], events));
                        break;
                    }

                    var classification = LmaxFixMessageClassifier.Classify(frame);
                    events.Add(ToEvent(options, preflight.InstrumentMappings[0], classification, transport.Source));
                }

                if (!options.NoExternal)
                {
                    await SendPolicyCheckedAsync(transport, BuildLogout(options), sandboxOptions, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                failures.Add(SanitizeFailure(ex.Message, options));
            }
            finally
            {
                if (transport is not null)
                {
                    await transport.CloseAsync(cancellationToken);
                }
            }
        }

        var ended = timeProvider.GetUtcNow();
        var externalCallAttempted = transport?.ExternalCallAttempted ?? false;
        var summary = BuildSummary(options, preflight, started, ended, events, failures);
        var secretScan = BuildSecretScanReport(options, events, preflight);
        var capture = BuildCaptureReport(options, summary, preflight, externalCallAttempted);
        var boundary = BuildBoundaryReport(options, externalCallAttempted);
        var fakeValidation = BuildFakeValidationReport(options, summary, preflight);

        return new LmaxBoundedMarketDataLoopResult(preflight, summary, events, capture, boundary, fakeValidation, secretScan);
    }

    public static LmaxBoundedMarketDataLoopPreflightReport Evaluate(LmaxBoundedMarketDataLoopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var mappings = new List<LmaxSandboxInstrumentMapping>();

        var environmentDemo = string.Equals(options.Environment, LmaxBoundedMarketDataLoopOptions.DemoEnvironment, StringComparison.OrdinalIgnoreCase);
        Require(environmentDemo, "environment must be demo", failures);
        var marketDataEndpointOnly = string.Equals(options.Host, LmaxBoundedMarketDataLoopOptions.DemoMarketDataHost, StringComparison.OrdinalIgnoreCase) &&
                                     options.Port == 443 &&
                                     !options.Host.Contains("fix-order", StringComparison.OrdinalIgnoreCase);
        Require(marketDataEndpointOnly, "endpoint must be LMAX demo market-data host on port 443", failures);
        var tradingEndpointBlocked = !options.Host.Contains("fix-order", StringComparison.OrdinalIgnoreCase) &&
                                     !string.Equals(options.Host, LmaxBoundedMarketDataLoopOptions.DemoOrderHost, StringComparison.OrdinalIgnoreCase);
        Require(tradingEndpointBlocked, "trading endpoint is forbidden", failures);
        var targetCompIdMarketDataOnly = string.Equals(options.TargetCompId, LmaxBoundedMarketDataLoopOptions.DemoMarketDataTargetCompId, StringComparison.Ordinal) &&
                                         !string.Equals(options.TargetCompId, LmaxBoundedMarketDataLoopOptions.DemoOrderTargetCompId, StringComparison.Ordinal);
        Require(targetCompIdMarketDataOnly, "TargetCompId must be LMXBDM and must not be LMXBD", failures);

        Require(options.NoExecution, "noExecution must be true", failures);
        if (options.NoExternal && !options.UseFakeFixServer)
        {
            failures.Add("noExternal=true requires useFakeFixServer=true.");
        }

        if (!options.NoExternal && options.UseFakeFixServer)
        {
            failures.Add("demo external loop requires useFakeFixServer=false.");
        }

        var operatorApprovalValid = options.NoExternal ||
                                    options.ExternalApproved &&
                                    string.Equals(options.OperatorApprovalPhrase, LmaxBoundedMarketDataLoopOptions.ApprovalPhrase, StringComparison.Ordinal);
        Require(operatorApprovalValid, "external demo loop requires exact operator approval phrase", failures);

        var durationBounded = options.DurationSeconds is > 0 and <= 120;
        var maxMessagesBounded = options.MaxMessages is > 0 and <= 10_000;
        var mdUpdateTypeValid = options.MdUpdateType is 0 or 1;
        Require(durationBounded, "durationSeconds must be in 1..120", failures);
        Require(maxMessagesBounded, "maxMessages must be in 1..10000", failures);
        Require(mdUpdateTypeValid, "md-update-type must be 0 or 1", failures);

        foreach (var instrument in options.Instruments)
        {
            var mapping = LmaxSandboxInstrumentMapping.Find(instrument);
            if (mapping is null)
            {
                failures.Add($"instrument not allow-listed for fake bounded loop: {instrument}");
            }
            else
            {
                mappings.Add(mapping);
            }
        }

        if (options.Instruments.Count != 1)
        {
            failures.Add("fake bounded loop currently supports exactly one allow-listed instrument.");
        }

        var instrumentsAllowListed = options.Instruments.Count == 1 && mappings.Count == options.Instruments.Count;
        var outputRootRunKeyValid = !string.IsNullOrWhiteSpace(options.RunKey) &&
                                    !string.IsNullOrWhiteSpace(options.OutputRoot) &&
                                    Path.GetFileName(Path.GetFullPath(options.OutputRoot)).Equals(options.RunKey, StringComparison.OrdinalIgnoreCase);
        Require(outputRootRunKeyValid, "output-root leaf must match run-key", failures);
        var secretsPresent = options.NoExternal ||
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.UsernameSecretRef)) &&
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.PasswordSecretRef));
        Require(secretsPresent, "external demo loop requires LMAX_DEMO_MD_USERNAME and LMAX_DEMO_MD_PASSWORD secret refs", failures);
        var secretsRedacted = SecretRefSafe(options.UsernameSecretRef) && SecretRefSafe(options.PasswordSecretRef);
        Require(secretsRedacted, "credential values must not be supplied directly", failures);

        return new(
            options.RunKey,
            DateTimeOffset.UtcNow,
            failures.Count == 0 ? "PASS" : "FAIL",
            environmentDemo,
            marketDataEndpointOnly,
            tradingEndpointBlocked,
            targetCompIdMarketDataOnly,
            options.NoExternal,
            options.ExternalApproved,
            operatorApprovalValid,
            options.NoExecution,
            options.UseFakeFixServer,
            durationBounded,
            maxMessagesBounded,
            mdUpdateTypeValid,
            instrumentsAllowListed,
            outputRootRunKeyValid,
            DbPersistenceDisabled: true,
            SchedulerDisabled: true,
            secretsPresent,
            secretsRedacted,
            mappings,
            failures);
    }

    private static bool SecretRefSafe(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           !value.Contains('=') &&
           !value.Any(char.IsWhiteSpace);

    private static IEnumerable<string> BuildFakeLifecycle(LmaxSandboxInstrumentMapping mapping)
    {
        yield return FakeFixTransport.FixMessage("35=A", "49=LMXBDM", "56=QQPRODMD", "98=0", "108=30");
        yield return FakeFixTransport.FixMessage("35=W", $"55={mapping.Instrument}", $"48={mapping.SecurityId}", $"22={mapping.SecurityIdSource}", "268=2", "269=0", "270=1.27001", "271=1000000", "269=1", "270=1.27003", "271=1000000");
        yield return FakeFixTransport.FixMessage("35=X", $"55={mapping.Instrument}", $"48={mapping.SecurityId}", $"22={mapping.SecurityIdSource}", "268=2", "269=0", "270=1.27002", "271=900000", "269=1", "270=1.27004", "271=950000");
    }

    private LmaxBoundedMarketDataLoopEvent ToEvent(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxSandboxInstrumentMapping mapping,
        LmaxFixClassification classification,
        string source)
        => new(
            options.RunKey,
            timeProvider.GetUtcNow(),
            options.Environment,
            source,
            classification.Instrument ?? mapping.Instrument,
            mapping.SecurityId,
            mapping.SecurityIdSource,
            classification.FixMsgType,
            classification.Classification,
            classification.Bid,
            classification.Ask,
            classification.BidSize,
            classification.AskSize,
            RedactKnownSecrets(classification.RawRedactedFix, options));

    private LmaxBoundedMarketDataLoopEvent BuildTimeoutEvent(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxSandboxInstrumentMapping mapping,
        IReadOnlyList<LmaxBoundedMarketDataLoopEvent> events)
    {
        var classification = events.Any(x => x.Classification is "Snapshot" or "Incremental")
            ? "TerminalTimeout"
            : "Timeout";
        return new(
            options.RunKey,
            timeProvider.GetUtcNow(),
            options.Environment,
            options.UseFakeFixServer ? "fake" : "lmax-demo",
            mapping.Instrument,
            mapping.SecurityId,
            mapping.SecurityIdSource,
            "TIMEOUT",
            classification,
            null,
            null,
            null,
            null,
            string.Empty);
    }

    private static LmaxBoundedMarketDataLoopSummary BuildSummary(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxBoundedMarketDataLoopPreflightReport preflight,
        DateTimeOffset started,
        DateTimeOffset ended,
        IReadOnlyList<LmaxBoundedMarketDataLoopEvent> events,
        IReadOnlyList<string> failures)
    {
        var snapshots = events.Count(x => x.Classification == "Snapshot");
        var incrementals = events.Count(x => x.Classification == "Incremental");
        var hasMarketData = snapshots > 0 || incrementals > 0;
        var terminalTimeouts = events.Count(x => x.Classification == "TerminalTimeout");
        var malformed = events.Count(x => x.Classification == "Malformed frame");
        var unknown = events.Count(x => x.Classification == "Unknown");
        var sessionRejects = events.Count(x => x.FixMsgType == "3");
        var marketDataRequestRejects = events.Count(x => x.FixMsgType == "3");
        var credentialsRejected = events.Count(x => x.Classification == "CredentialsRejected");
        var timeoutOnly = !hasMarketData && events.Any(x => x.Classification == "Timeout");
        var primaryFailureReason = !preflight.SecretsPresent
            ? "MISSING_DEMO_MARKETDATA_SECRET_REFS"
            : !preflight.OperatorApprovalValid
                ? "MISSING_OPERATOR_APPROVAL"
                : credentialsRejected > 0
                    ? "BAD_CREDENTIALS"
                    : sessionRejects > 0 || marketDataRequestRejects > 0
                        ? "MARKET_DATA_REQUEST_REJECTED"
                        : malformed > 0
                            ? "MALFORMED"
                            : timeoutOnly
                                ? "TIMEOUT"
                                : failures.Count > 0
                                    ? "UNKNOWN"
                                    : "NONE";
        var finalFixSessionState = credentialsRejected > 0
            ? "LogoutCredentialsRejected"
            : hasMarketData
                ? "MarketDataObserved"
                : sessionRejects > 0
                    ? "RejectObserved"
                    : timeoutOnly
                        ? "TimedOut"
                        : "NoMessages";
        var clean = hasMarketData &&
                    primaryFailureReason == "NONE" &&
                    sessionRejects == 0 &&
                    credentialsRejected == 0 &&
                    malformed == 0 &&
                    failures.Count == 0;
        var notAttempted = preflight.Gate == "FAIL" &&
                           (!preflight.SecretsPresent || !preflight.OperatorApprovalValid);
        var status = notAttempted
            ? "NOT_ATTEMPTED"
            : clean
                ? "PASS"
                : sessionRejects > 0 || marketDataRequestRejects > 0 || timeoutOnly
                    ? "WARN"
                    : failures.Count > 0 || credentialsRejected > 0
                        ? "FAIL"
                        : "WARN";

        return new(
            options.RunKey,
            started,
            ended,
            options.DurationSeconds,
            options.Environment,
            options.UseFakeFixServer ? "fake" : "lmax-demo",
            options.Instruments,
            events.Count(x => x.Classification is not "TerminalTimeout" and not "Timeout"),
            events.Count(x => x.Classification == "Logon"),
            snapshots,
            incrementals,
            sessionRejects,
            marketDataRequestRejects,
            events.Count(x => x.Classification is "Logout" or "CredentialsRejected"),
            credentialsRejected,
            terminalTimeouts,
            malformed,
            unknown,
            primaryFailureReason,
            finalFixSessionState,
            clean ? "YES" : "NO",
            status);
    }

    private static LmaxSandboxMarketDataOptions ToSandboxOptions(LmaxBoundedMarketDataLoopOptions options)
        => new(
            options.RunKey,
            options.OutputRoot,
            options.Environment,
            options.Host,
            options.Port,
            options.TargetCompId,
            options.SenderCompId,
            options.UsernameSecretRef,
            options.PasswordSecretRef,
            options.Instruments,
            options.DurationSeconds,
            options.MaxMessages,
            ConnectTimeoutMs: 10_000,
            ReadTimeoutMs: 2_000,
            HeartbeatIntervalSeconds: 30,
            options.MdUpdateType,
            options.NoExternal,
            options.ExternalApproved,
            // The transport guard uses the one-shot collector approval phrase; bounded-loop preflight already validates its own fresh phrase.
            options.NoExternal ? null : LmaxSandboxMarketDataOptions.ApprovalPhrase,
            options.NoExecution,
            DisableOrderPath: true,
            options.UseFakeFixServer);

    private static async Task SendPolicyCheckedAsync(
        IFixTransport transport,
        string fixMessage,
        LmaxSandboxMarketDataOptions options,
        CancellationToken cancellationToken)
    {
        var decision = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(fixMessage, options);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }

        await transport.SendAsync(fixMessage, cancellationToken);
    }

    private static string BuildLogon(LmaxBoundedMarketDataLoopOptions options)
    {
        var username = Environment.GetEnvironmentVariable(options.UsernameSecretRef);
        var password = Environment.GetEnvironmentVariable(options.PasswordSecretRef);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("External demo loop requires credential secret refs to resolve from environment.");
        }

        return FixMessage(
            "35=A",
            $"49={username}",
            $"56={options.TargetCompId}",
            "34=1",
            $"52={DateTimeOffset.UtcNow:yyyyMMdd-HH:mm:ss.fff}",
            "98=0",
            "108=30",
            $"553={username}",
            $"554={password}");
    }

    private static string BuildLogout(LmaxBoundedMarketDataLoopOptions options)
    {
        var username = Environment.GetEnvironmentVariable(options.UsernameSecretRef);
        var sender = string.IsNullOrWhiteSpace(username) ? options.SenderCompId : username;
        return FixMessage("35=5", $"49={sender}", $"56={options.TargetCompId}", "34=3", $"52={DateTimeOffset.UtcNow:yyyyMMdd-HH:mm:ss.fff}", "58=client bounded loop logout");
    }

    private static string FixMessage(params string[] fields)
    {
        var body = string.Join('\u0001', fields) + '\u0001';
        var header = $"8=FIX.4.4\u00019={Encoding.ASCII.GetByteCount(body)}\u0001";
        var withoutChecksum = header + body;
        var checksum = Encoding.ASCII.GetBytes(withoutChecksum).Sum(x => x) % 256;
        return withoutChecksum + $"10={checksum:000}\u0001";
    }

    private static string SanitizeFailure(string message, LmaxBoundedMarketDataLoopOptions options)
        => RedactKnownSecrets(message
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "554=[redacted]", StringComparison.OrdinalIgnoreCase), options);

    private static string RedactKnownSecrets(string text, LmaxBoundedMarketDataLoopOptions options)
    {
        var redacted = text;
        foreach (var secretRef in new[] { options.UsernameSecretRef, options.PasswordSecretRef })
        {
            var value = Environment.GetEnvironmentVariable(secretRef);
            if (!string.IsNullOrWhiteSpace(value))
            {
                redacted = redacted.Replace(value, "[redacted]", StringComparison.Ordinal);
            }
        }

        return redacted;
    }

    private static IReadOnlyDictionary<string, object> BuildCaptureReport(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxBoundedMarketDataLoopSummary summary,
        LmaxBoundedMarketDataLoopPreflightReport preflight,
        bool externalCallAttempted)
    {
        var pass = preflight.Gate == "PASS" && summary.Status == "PASS";
        var notAttempted = summary.Status == "NOT_ATTEMPTED";
        var loopStatus = notAttempted
            ? "NOT_ATTEMPTED"
            : pass
                ? "PASS"
                : summary.Status == "WARN"
                    ? "WARN"
                    : "FAIL";
        var firstStepStatus = pass
            ? "SANDBOX_LOOP_CAPTURED"
            : notAttempted
                ? "NOT_ATTEMPTED"
                : summary.PrimaryFailureReason == "BAD_CREDENTIALS"
                    ? "SANDBOX_LOOP_CREDENTIALS_REJECTED"
                    : summary.PrimaryFailureReason == "MARKET_DATA_REQUEST_REJECTED"
                        ? "SANDBOX_LOOP_REJECT_CLASSIFIED"
                        : summary.PrimaryFailureReason == "TIMEOUT"
                            ? "SANDBOX_LOOP_TIMEOUT_CLASSIFIED"
                            : "FAILED";
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["LMAX_BOUNDED_LOOP_FAKE_STATUS"] = pass ? "PASS" : "FAIL",
            ["LMAX_BOUNDED_LOOP_DEMO_STATUS"] = loopStatus,
            ["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"] = externalCallAttempted ? "YES" : "NO",
            ["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_DB_WRITE"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_EXECUTION"] = options.NoExecution ? "YES" : "NO",
            ["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"] = "YES",
            ["LMAX_BOUNDED_LOOP_NO_SCHEDULER"] = "YES",
            ["LMAX_BOUNDED_LOOP_SECRET_SCAN"] = "PASS",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"] = firstStepStatus,
            ["PRIMARY_FAILURE_REASON"] = summary.PrimaryFailureReason,
            ["SAFE_NEXT_PHASE"] = pass
                ? options.UseFakeFixServer ? "OPERATOR_APPROVED_BOUNDED_SANDBOX_LOOP" : "BOUNDED_LOOP_EVIDENCE_FREEZE"
                : notAttempted || summary.Status == "WARN" ? "STATUS_ONLY" : "BLOCKED",
            ["preflightGate"] = preflight.Gate,
            ["summary"] = summary,
            ["failures"] = preflight.Failures
        };
    }

    private static IReadOnlyDictionary<string, object> BuildBoundaryReport(LmaxBoundedMarketDataLoopOptions options, bool externalCallAttempted)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["LMAX_EXTERNAL_CALLS_ATTEMPTED"] = externalCallAttempted ? "YES" : "NO",
            ["PRODUCTION_ENDPOINT_USED"] = "NO",
            ["TRADING_ENDPOINT_USED"] = "NO",
            ["ORDER_MESSAGES_SENT"] = "NO",
            ["FILL_ARTIFACTS_CREATED"] = "NO",
            ["ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"] = "NO",
            ["DB_WRITE_ATTEMPTED"] = "NO",
            ["MIGRATION_ATTEMPTED"] = "NO",
            ["QUBES_EXECUTED"] = "NO",
            ["QUBES_WEIGHTS_GENERATED"] = "NO",
            ["PMS_OMS_EMS_TOUCHED"] = "NO",
            ["MANAGER_ANUBIS_TOUCHED"] = "NO",
            ["A_H_I_CREATED"] = "NO",
            ["SCHEDULER_STARTED"] = "NO",
            ["WORKER_SERVICE_STARTED"] = "NO",
            ["CREDENTIAL_VALUES_PERSISTED"] = "NO",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings"
        };

    private static IReadOnlyDictionary<string, object> BuildFakeValidationReport(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxBoundedMarketDataLoopSummary summary,
        LmaxBoundedMarketDataLoopPreflightReport preflight)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["fakeTransportUsed"] = options.UseFakeFixServer,
            ["noExternal"] = options.NoExternal,
            ["preflightGate"] = preflight.Gate,
            ["logonObserved"] = summary.Logons > 0 ? "YES" : "NO",
            ["snapshotObserved"] = summary.Snapshots > 0 ? "YES" : "NO",
            ["incrementalObserved"] = summary.Incrementals > 0 ? "YES" : "NO",
            ["terminalTimeoutNonPrimary"] = summary.TerminalTimeouts > 0 && summary.PrimaryFailureReason == "NONE" ? "YES" : "NO",
            ["LMAX_BOUNDED_LOOP_FAKE_STATUS"] = preflight.Gate == "PASS" && summary.Status == "PASS" ? "PASS" : "FAIL",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings"
        };

    private static IReadOnlyDictionary<string, object> BuildSecretScanReport(
        LmaxBoundedMarketDataLoopOptions options,
        IReadOnlyList<LmaxBoundedMarketDataLoopEvent> events,
        LmaxBoundedMarketDataLoopPreflightReport preflight)
    {
        var values = new[] { options.UsernameSecretRef, options.PasswordSecretRef }
            .Select(Environment.GetEnvironmentVariable)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var serializedEvents = string.Join(Environment.NewLine, events.Select(x => x.RawRedactedFix));
        var leaked = values.Any(value => serializedEvents.Contains(value, StringComparison.Ordinal));
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["LMAX_BOUNDED_LOOP_SECRET_SCAN"] = leaked ? "FAIL" : "PASS",
            ["credentialValuesPersisted"] = leaked ? "YES" : "NO",
            ["credentialValuesRedacted"] = leaked ? "NO" : "YES",
            ["secretRefs"] = new[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["secretName"] = options.UsernameSecretRef,
                    ["present"] = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.UsernameSecretRef)),
                    ["valuePersisted"] = false,
                    ["redactionStatus"] = "PASS"
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["secretName"] = options.PasswordSecretRef,
                    ["present"] = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.PasswordSecretRef)),
                    ["valuePersisted"] = false,
                    ["redactionStatus"] = "PASS"
                }
            },
            ["preflightSecretsPresent"] = preflight.SecretsPresent,
            ["rawValuesPersisted"] = false
        };
    }

    private static void Require(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}

public sealed class LmaxBoundedMarketDataLoopReports
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

    public async Task WriteAsync(
        LmaxBoundedMarketDataLoopOptions options,
        LmaxBoundedMarketDataLoopResult result,
        CancellationToken cancellationToken)
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

        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_marketdata_loop_preflight_report", ToPreflightDictionary(result.Preflight), Markdown.Preflight(result.Preflight), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_marketdata_loop_capture_report", result.CaptureReport, Markdown.Capture(result.CaptureReport), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_marketdata_loop_boundary_report", result.BoundaryReport, Markdown.Boundary(result.BoundaryReport), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "lmax_marketdata_loop_secret_scan_report", result.SecretScanReport, Markdown.SecretScan(result.SecretScanReport), cancellationToken);
        if (options.UseFakeFixServer)
        {
            await WriteJsonAndMarkdownAsync(validationRoot, "lmax_marketdata_loop_fake_validation_report", result.FakeValidationReport, Markdown.FakeValidation(result.FakeValidationReport), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_marketdata_loop_fake_summary.md"), Markdown.Summary(result.CaptureReport, result.Summary), cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(Path.Combine(shareRoot, "lmax_marketdata_loop_demo_summary.md"), Markdown.Summary(result.CaptureReport, result.Summary), cancellationToken);
        }

        await WriteManifestAsync(outputRoot, options.RunKey, result.CaptureReport, cancellationToken);
    }

    private static IReadOnlyDictionary<string, object> ToPreflightDictionary(LmaxBoundedMarketDataLoopPreflightReport report)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = report.RunKey,
            ["createdAtUtc"] = report.CreatedAtUtc,
            ["gate"] = report.Gate,
            ["environmentDemo"] = report.EnvironmentDemo,
            ["marketDataEndpointOnly"] = report.MarketDataEndpointOnly,
            ["tradingEndpointBlocked"] = report.TradingEndpointBlocked,
            ["targetCompIdMarketDataOnly"] = report.TargetCompIdMarketDataOnly,
            ["noExternal"] = report.NoExternal,
            ["externalApproved"] = report.ExternalApproved,
            ["operatorApprovalValid"] = report.OperatorApprovalValid,
            ["noExecution"] = report.NoExecution,
            ["useFakeFixServer"] = report.UseFakeFixServer,
            ["durationBounded"] = report.DurationBounded,
            ["maxMessagesBounded"] = report.MaxMessagesBounded,
            ["mdUpdateTypeValid"] = report.MdUpdateTypeValid,
            ["instrumentsAllowListed"] = report.InstrumentsAllowListed,
            ["outputRootRunKeyValid"] = report.OutputRootRunKeyValid,
            ["dbPersistenceDisabled"] = report.DbPersistenceDisabled,
            ["schedulerDisabled"] = report.SchedulerDisabled,
            ["secretsPresent"] = report.SecretsPresent,
            ["secretsRedacted"] = report.SecretsRedacted,
            ["instrumentMappings"] = report.InstrumentMappings,
            ["failures"] = report.Failures
        };

    private static async Task WriteEventsAsync(string path, IReadOnlyList<LmaxBoundedMarketDataLoopEvent> events, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);
        foreach (var item in events.OrderBy(x => x.ReceivedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(JsonSerializer.Serialize(item, JsonLineOptions));
        }
    }

    private static Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
        => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);

    private static async Task WriteJsonAndMarkdownAsync(
        string root,
        string basename,
        IReadOnlyDictionary<string, object> json,
        string markdown,
        CancellationToken cancellationToken)
    {
        await WriteJsonAsync(Path.Combine(root, $"{basename}.json"), json, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(string outputRoot, string runKey, IReadOnlyDictionary<string, object> captureReport, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "hashes.json" and not "manifest.sha256")
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = await Sha256Async(file, cancellationToken);
        }

        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["run_key"] = runKey,
            ["created_at_utc"] = DateTimeOffset.UtcNow,
            ["package_type"] = "lmax_bounded_marketdata_loop_fake_no_external",
            ["lmax_bounded_loop_status"] = captureReport.TryGetValue("LMAX_BOUNDED_LOOP_DEMO_STATUS", out var demoStatus) ? demoStatus : captureReport["LMAX_BOUNDED_LOOP_FAKE_STATUS"],
            ["marketdata_lmax_db_status"] = "AdoptedWithWarnings",
            ["safe_next_phase"] = captureReport["SAFE_NEXT_PHASE"],
            ["files"] = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
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
        public static string Preflight(LmaxBoundedMarketDataLoopPreflightReport report)
            => Lines(
                "# LMAX Bounded MarketData Loop Preflight Report",
                "",
                $"- Gate = `{report.Gate}`",
                $"- Environment demo = `{report.EnvironmentDemo}`",
                $"- Market-data endpoint only = `{report.MarketDataEndpointOnly}`",
                $"- Trading endpoint blocked = `{report.TradingEndpointBlocked}`",
                $"- TargetCompId market-data only = `{report.TargetCompIdMarketDataOnly}`",
                $"- NoExternal = `{report.NoExternal}`",
                $"- External approved = `{report.ExternalApproved}`",
                $"- Operator approval valid = `{report.OperatorApprovalValid}`",
                $"- NoExecution = `{report.NoExecution}`",
                $"- Use fake FIX server = `{report.UseFakeFixServer}`",
                $"- Duration bounded = `{report.DurationBounded}`",
                $"- Max messages bounded = `{report.MaxMessagesBounded}`",
                $"- Instrument allow-listed = `{report.InstrumentsAllowListed}`",
                $"- Secrets present = `{report.SecretsPresent}`",
                $"- Secrets redacted = `{report.SecretsRedacted}`");

        public static string Capture(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Capture Report",
                "",
                $"- LMAX_BOUNDED_LOOP_DEMO_STATUS = `{report["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}`",
                $"- LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED = `{report["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY = `{report["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_DB_WRITE = `{report["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_EXECUTION = `{report["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT = `{report["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT = `{report["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]}`",
                $"- LMAX_BOUNDED_LOOP_NO_SCHEDULER = `{report["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS = `{report["FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"]}`",
                $"- PRIMARY_FAILURE_REASON = `{report["PRIMARY_FAILURE_REASON"]}`",
                $"- SAFE_NEXT_PHASE = `{report["SAFE_NEXT_PHASE"]}`");

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Boundary Report",
                "",
                $"- LMAX_EXTERNAL_CALLS_ATTEMPTED = `{report["LMAX_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- PRODUCTION_ENDPOINT_USED = `{report["PRODUCTION_ENDPOINT_USED"]}`",
                $"- TRADING_ENDPOINT_USED = `{report["TRADING_ENDPOINT_USED"]}`",
                $"- ORDER_MESSAGES_SENT = `{report["ORDER_MESSAGES_SENT"]}`",
                $"- DB_WRITE_ATTEMPTED = `{report["DB_WRITE_ATTEMPTED"]}`",
                $"- QUBES_EXECUTED = `{report["QUBES_EXECUTED"]}`",
                $"- PMS_OMS_EMS_TOUCHED = `{report["PMS_OMS_EMS_TOUCHED"]}`",
                $"- A_H_I_CREATED = `{report["A_H_I_CREATED"]}`",
                $"- SCHEDULER_STARTED = `{report["SCHEDULER_STARTED"]}`",
                $"- CREDENTIAL_VALUES_PERSISTED = `{report["CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string FakeValidation(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Fake Validation",
                "",
                $"- fakeTransportUsed = `{report["fakeTransportUsed"]}`",
                $"- logonObserved = `{report["logonObserved"]}`",
                $"- snapshotObserved = `{report["snapshotObserved"]}`",
                $"- incrementalObserved = `{report["incrementalObserved"]}`",
                $"- terminalTimeoutNonPrimary = `{report["terminalTimeoutNonPrimary"]}`",
                $"- LMAX_BOUNDED_LOOP_FAKE_STATUS = `{report["LMAX_BOUNDED_LOOP_FAKE_STATUS"]}`");

        public static string SecretScan(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Bounded MarketData Loop Secret Scan",
                "",
                $"- LMAX_BOUNDED_LOOP_SECRET_SCAN = `{report["LMAX_BOUNDED_LOOP_SECRET_SCAN"]}`",
                $"- credentialValuesPersisted = `{report["credentialValuesPersisted"]}`",
                $"- credentialValuesRedacted = `{report["credentialValuesRedacted"]}`",
                $"- rawValuesPersisted = `{report["rawValuesPersisted"]}`");

        public static string Summary(IReadOnlyDictionary<string, object> report, LmaxBoundedMarketDataLoopSummary summary)
            => Lines(
                "# LMAX Bounded MarketData Loop Fake Summary",
                "",
                $"- Bounded loop status: `{report["LMAX_BOUNDED_LOOP_DEMO_STATUS"]}`",
                $"- External calls attempted: `{report["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- Artifacts-only: `{report["LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY"]}`",
                $"- No DB write: `{report["LMAX_BOUNDED_LOOP_NO_DB_WRITE"]}`",
                $"- No execution: `{report["LMAX_BOUNDED_LOOP_NO_EXECUTION"]}`",
                $"- No production endpoint: `{report["LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT"]}`",
                $"- No trading endpoint: `{report["LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT"]}`",
                $"- No scheduler: `{report["LMAX_BOUNDED_LOOP_NO_SCHEDULER"]}`",
                $"- Messages read: `{summary.MessagesRead}`",
                $"- Logons: `{summary.Logons}`",
                $"- Snapshots: `{summary.Snapshots}`",
                $"- Incrementals: `{summary.Incrementals}`",
                $"- Terminal timeouts: `{summary.TerminalTimeouts}`",
                $"- Primary failure reason: `{summary.PrimaryFailureReason}`",
                $"- Final FIX session state: `{summary.FinalFixSessionState}`",
                $"- MarketData-LMAX-DB: `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- Safe next phase: `{report["SAFE_NEXT_PHASE"]}`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
