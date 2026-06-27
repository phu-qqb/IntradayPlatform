using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

public enum LmaxMarketDataOnlyGuardStatus
{
    Allowed,
    FailClosed
}

public sealed record LmaxMarketDataOnlyGuardResult(
    LmaxMarketDataOnlyGuardStatus Status,
    string MsgType,
    string Reason,
    bool SessionMustStop)
{
    public bool Allowed => Status == LmaxMarketDataOnlyGuardStatus.Allowed;
}

public static class LmaxMarketDataOnlyOutboundFixMessageGuard
{
    public static readonly IReadOnlySet<string> AllowedOutboundMsgTypes = ImmutableHashSet.CreateRange(StringComparer.Ordinal, ["A", "0", "1", "2", "4", "5", "V"]);
    public static readonly IReadOnlySet<string> ForbiddenOutboundMsgTypes = ImmutableHashSet.CreateRange(StringComparer.Ordinal, ["D", "F", "G", "H", "q", "AF", "AE", "AD", "8"]);

    public static LmaxMarketDataOnlyGuardResult InspectOutboundMessage(string fixMessage)
    {
        var msgType = LmaxFixMarketDataCodec.GetMsgType(fixMessage) ?? "(missing)";
        return InspectMsgType(msgType);
    }

    public static LmaxMarketDataOnlyGuardResult InspectMsgType(string? msgType)
    {
        if (string.IsNullOrWhiteSpace(msgType))
        {
            return Fail("(missing)", "missing_msg_type");
        }

        if (AllowedOutboundMsgTypes.Contains(msgType))
        {
            return new LmaxMarketDataOnlyGuardResult(LmaxMarketDataOnlyGuardStatus.Allowed, msgType, "read_only_market_data_session_msg_type_allowed", SessionMustStop: false);
        }

        if (ForbiddenOutboundMsgTypes.Contains(msgType))
        {
            return Fail(msgType, "forbidden_order_or_recovery_msg_type");
        }

        return Fail(msgType, "unknown_msg_type_fail_closed");
    }

    private static LmaxMarketDataOnlyGuardResult Fail(string msgType, string reason)
        => new(LmaxMarketDataOnlyGuardStatus.FailClosed, msgType, reason, SessionMustStop: true);
}

public sealed record LmaxMarketDataOnlyInstrument(string InstrumentId, string Symbol, string LmaxSlashSymbol, string SecurityIdSource = "8");

public sealed record LmaxMarketDataOnlyRawFixFrame(
    string RawFixMessage,
    DateTimeOffset SocketReceiveUtc,
    long LocalMonotonicTimestamp,
    string SessionAlias = "LMAX_DEMO_MARKET_DATA_ONLY",
    string SessionInstanceId = "M2C1A_FAKE_SESSION",
    string Environment = "DEMO",
    string Venue = "LMAX_DEMO_READ_ONLY");

public sealed record LmaxMarketDataOnlyBookValidation(
    bool BookValid,
    string Reason,
    decimal? BidPrice,
    decimal? BidQuantity,
    decimal? AskPrice,
    decimal? AskQuantity,
    string GapStatus);

public sealed class LmaxMarketDataOnlyObservationMapper
{
    public const string ParserVersion = "M2C1A_REUSED_CONNECTIVITY_LAB_LMAX_FIX_MARKET_DATA_CODEC_V1";
    private readonly IReadOnlyDictionary<string, LmaxMarketDataOnlyInstrument> instrumentsBySecurityId;
    private readonly IReadOnlyDictionary<string, LmaxMarketDataOnlyInstrument> instrumentsBySymbol;

    public LmaxMarketDataOnlyObservationMapper(IReadOnlyList<LmaxMarketDataOnlyInstrument> instruments)
    {
        instrumentsBySecurityId = instruments.ToDictionary(x => x.InstrumentId, StringComparer.OrdinalIgnoreCase);
        instrumentsBySymbol = instruments.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);
    }

    public ReadOnlyMarketDataObservationV2 Map(LmaxMarketDataOnlyRawFixFrame frame, IReadOnlySet<string> activeSubscriptions, long? previousSeqNum = null)
    {
        var msgType = LmaxFixMarketDataCodec.GetMsgType(frame.RawFixMessage) ?? "(missing)";
        var seq = ParseLong(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage, "34"));
        var possDup = string.Equals(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage, "43"), "Y", StringComparison.OrdinalIgnoreCase);
        var securityId = LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage, "48") ?? string.Empty;
        var symbol = NormalizeSymbol(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage, "55"));
        var instrument = ResolveInstrument(securityId, symbol);
        var entries = LmaxFixMarketDataCodec.ParseMarketDataEntries(frame.RawFixMessage);
        var sourceTime = ParseFixUtc(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage, "52")) ?? frame.SocketReceiveUtc;
        var book = ValidateBook(instrument, entries, activeSubscriptions);
        var quoteEventId = $"lmax-md-{frame.SessionInstanceId}-{seq}-{instrument?.InstrumentId ?? securityId}-{CanonicalRecorderV2.Sha256Text(frame.RawFixMessage)[..12]}";
        var gapStatus = DetermineGap(seq, possDup, previousSeqNum, book.GapStatus);

        return new ReadOnlyMarketDataObservationV2(
            frame.Environment,
            frame.Venue,
            frame.SessionAlias,
            frame.SessionInstanceId,
            instrument?.InstrumentId ?? securityId,
            instrument?.Symbol ?? symbol ?? "UNKNOWN",
            $"35={msgType}",
            sourceTime,
            frame.SocketReceiveUtc,
            frame.LocalMonotonicTimestamp,
            seq,
            possDup,
            quoteEventId,
            book.BidPrice ?? 0m,
            book.BidQuantity ?? 0m,
            book.AskPrice ?? 0m,
            book.AskQuantity ?? 0m,
            book.BookValid,
            gapStatus,
            instrument is not null && activeSubscriptions.Contains(instrument.Symbol) ? "SUBSCRIBED" : "NOT_SUBSCRIBED",
            CanonicalRecorderV2.Sha256Text(LmaxFixMarketDataCodec.SanitizeMessage(frame.RawFixMessage)),
            ParserVersion);
    }

    public ReadOnlyMarketDataObservationV1 ToV1(ReadOnlyMarketDataObservationV2 observation)
        => new(
            observation.Environment,
            observation.Venue,
            observation.SessionInstanceId,
            observation.InstrumentId,
            observation.Symbol,
            observation.SourceMessageType,
            observation.SourceTimestampUtc,
            observation.SocketReceiveUtc,
            observation.LocalMonotonicTimestamp,
            observation.FixMsgSeqNum,
            observation.PossDup,
            observation.QuoteEventId,
            observation.BidPrice,
            observation.BidQuantity,
            observation.AskPrice,
            observation.AskQuantity,
            observation.BookValid,
            observation.GapStatus,
            observation.SubscriptionState,
            observation.RawPayloadSha256);

    private LmaxMarketDataOnlyInstrument? ResolveInstrument(string? securityId, string? symbol)
    {
        if (!string.IsNullOrWhiteSpace(securityId) && instrumentsBySecurityId.TryGetValue(securityId, out var byId))
        {
            return byId;
        }

        if (!string.IsNullOrWhiteSpace(symbol) && instrumentsBySymbol.TryGetValue(symbol, out var bySymbol))
        {
            return bySymbol;
        }

        return null;
    }

    private static LmaxMarketDataOnlyBookValidation ValidateBook(LmaxMarketDataOnlyInstrument? instrument, IReadOnlyList<LmaxFixMarketDataEntry> entries, IReadOnlySet<string> activeSubscriptions)
    {
        if (instrument is null)
        {
            return Invalid("UNKNOWN_INSTRUMENT");
        }

        if (!activeSubscriptions.Contains(instrument.Symbol))
        {
            return Invalid("INSTRUMENT_NOT_SUBSCRIBED");
        }

        var top = LmaxFixMarketDataCodec.ComputeTopOfBook(entries);
        var bidQty = entries.Where(x => x.EntryType == "0" && x.Size.HasValue).Select(x => x.Size!.Value).DefaultIfEmpty(0m).Max();
        var askQty = entries.Where(x => x.EntryType == "1" && x.Size.HasValue).Select(x => x.Size!.Value).DefaultIfEmpty(0m).Max();
        if (!top.BestBid.HasValue || !top.BestAsk.HasValue)
        {
            return Invalid("MISSING_BID_OR_ASK", top.BestBid, bidQty, top.BestAsk, askQty);
        }

        if (top.BestBid <= 0 || top.BestAsk <= 0 || top.BestBid >= top.BestAsk)
        {
            return Invalid("INVALID_OR_CROSSED_BOOK", top.BestBid, bidQty, top.BestAsk, askQty);
        }

        if (bidQty < 0 || askQty < 0)
        {
            return Invalid("NEGATIVE_QUANTITY", top.BestBid, bidQty, top.BestAsk, askQty);
        }

        return new LmaxMarketDataOnlyBookValidation(true, "VALID_BBO", top.BestBid, bidQty, top.BestAsk, askQty, "OK");
    }

    private static LmaxMarketDataOnlyBookValidation Invalid(string reason, decimal? bid = null, decimal? bidQty = null, decimal? ask = null, decimal? askQty = null)
        => new(false, reason, bid, bidQty, ask, askQty, "INVALID_BOOK");

    private static string DetermineGap(long seq, bool possDup, long? previousSeqNum, string current)
    {
        if (!previousSeqNum.HasValue || possDup)
        {
            return current;
        }

        if (seq == previousSeqNum.Value)
        {
            return "DUPLICATE_SEQ";
        }

        if (seq < previousSeqNum.Value)
        {
            return "OUT_OF_ORDER";
        }

        return seq > previousSeqNum.Value + 1 ? "GAP" : current;
    }

    private static long ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static DateTimeOffset? ParseFixUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyyMMdd-HH:mm:ss.fff", "yyyyMMdd-HH:mm:ss" };
        return DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeSymbol(string? symbol)
        => string.IsNullOrWhiteSpace(symbol) ? null : symbol.Replace("/", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

public sealed class LmaxMarketDataOnlyFakeSource(IReadOnlyList<LmaxMarketDataOnlyInstrument> instruments, IReadOnlyList<LmaxMarketDataOnlyRawFixFrame> frames) : IReadOnlyMarketDataSource
{
    private readonly ReadOnlyMarketDataFeedStateMachine machine = new();
    private readonly LmaxMarketDataOnlyObservationMapper mapper = new(instruments);
    private readonly HashSet<string> activeSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private bool started;

    public ReadOnlyMarketDataHealth Health => new(machine.State, machine.State == ReadOnlyMarketDataFeedState.Synchronized, machine.Reason);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        machine.OnStart();
        machine.OnConnected();
        started = true;
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IReadOnlyList<ReadOnlyMarketDataSubscription> subscriptions, CancellationToken cancellationToken = default)
    {
        if (!started)
        {
            machine.OnFailed("subscribe_before_start");
            return Task.CompletedTask;
        }

        machine.OnSubscribing(subscriptions);
        activeSubscriptions.Clear();
        foreach (var subscription in subscriptions)
        {
            activeSubscriptions.Add(subscription.Symbol);
        }

        machine.OnSynchronized();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ReadOnlyMarketDataObservationV1> ReadMarketDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long? previousSeq = null;
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var v2 = mapper.Map(frame, activeSubscriptions, previousSeq);
            var v1 = mapper.ToV1(v2);
            if (v1.GapStatus is "GAP" or "OUT_OF_ORDER")
            {
                machine.OnGap();
            }
            else if (!v1.BookValid)
            {
                machine.OnFailed(v2.GapStatus == "INVALID_BOOK" ? "invalid_book" : v2.GapStatus.ToLowerInvariant());
            }
            else if (machine.State is ReadOnlyMarketDataFeedState.GapDetected or ReadOnlyMarketDataFeedState.Recovering)
            {
                machine.OnRecovering();
            }
            else
            {
                machine.OnSynchronized();
            }

            previousSeq = Math.Max(previousSeq ?? 0, v2.FixMsgSeqNum);
            await Task.Yield();
            yield return v1;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        machine.OnStopping();
        machine.OnStopped();
        return Task.CompletedTask;
    }
}

public sealed record LmaxMarketDataOnlyPreflightConfig(
    string Mode,
    string Environment,
    string Venue,
    string MarketDataEndpointAlias,
    string MarketDataSessionAlias,
    string MarketDataCredentialReference,
    string CredentialScope,
    IReadOnlyList<string> Instruments,
    string OutputRoot,
    int MaxDurationSeconds,
    int MaxEvents,
    long MaxTotalBytes,
    long MinimumFreeDiskBytes,
    int QuoteAgeThresholdMs,
    long RotateAfterBytes,
    int FlushIntervalMs,
    IReadOnlyList<string> AllowedOutboundFixMsgTypes,
    string ToolCommit,
    string ConfigHash);

public sealed record LmaxMarketDataOnlyPreflightReport(string Status, IReadOnlyList<string> Issues, string ConfigHash, bool NetworkDisabled, bool OrderEntryDisabled, bool AccountApiDisabled, bool DbDisabled);

public static class LmaxMarketDataOnlyPreflight
{
    private static readonly HashSet<string> AllowedEndpointAliases = new(StringComparer.OrdinalIgnoreCase) { "LMAX_DEMO_MARKET_DATA_ONLY" };
    private static readonly HashSet<string> AllowedSessionAliases = new(StringComparer.OrdinalIgnoreCase) { "LMAX_DEMO_MD_READ_ONLY" };
    private static readonly HashSet<string> AllowedInstruments = new(StringComparer.OrdinalIgnoreCase) { "EURUSD" };

    public static LmaxMarketDataOnlyPreflightReport Validate(
        LmaxMarketDataOnlyPreflightConfig config,
        bool networkDisabled,
        bool noOrderEntry,
        bool noAccountApi,
        bool noDb,
        bool outputRootMustBeEmpty = true,
        JsonElement? configDocument = null)
    {
        var issues = new List<string>();
        if (config.Mode != "CAPTURE_ONLY") issues.Add("mode_must_be_CAPTURE_ONLY");
        if (config.Environment != "DEMO") issues.Add("first_capture_environment_must_be_DEMO");
        if (!AllowedEndpointAliases.Contains(config.MarketDataEndpointAlias)) issues.Add("endpoint_alias_not_allowlisted");
        if (!AllowedSessionAliases.Contains(config.MarketDataSessionAlias)) issues.Add("session_alias_not_allowlisted");
        if (config.CredentialScope != "MARKET_DATA_ONLY") issues.Add("credential_scope_must_be_MARKET_DATA_ONLY");
        if (!networkDisabled) issues.Add("m2c1a_network_disabled_flag_required");
        if (!noOrderEntry) issues.Add("order_entry_disabled_flag_required");
        if (!noAccountApi) issues.Add("account_api_disabled_flag_required");
        if (!noDb) issues.Add("db_disabled_flag_required");
        if (string.IsNullOrWhiteSpace(config.ToolCommit)) issues.Add("tool_commit_required");
        if (string.IsNullOrWhiteSpace(config.ConfigHash)) issues.Add("config_hash_required");
        if (config.MaxDurationSeconds <= 0) issues.Add("max_duration_seconds_required");
        if (config.MaxEvents <= 0) issues.Add("max_events_required");
        if (config.MaxTotalBytes <= 0) issues.Add("max_total_bytes_required");
        if (config.MinimumFreeDiskBytes <= 0) issues.Add("minimum_free_disk_bytes_required");
        if (config.RotateAfterBytes <= 0) issues.Add("rotate_after_bytes_required");
        if (config.FlushIntervalMs <= 0) issues.Add("flush_interval_ms_required");
        if (string.IsNullOrWhiteSpace(config.OutputRoot)) issues.Add("output_root_required");
        else if (!HasRequiredFreeDisk(config.OutputRoot, config.MinimumFreeDiskBytes)) issues.Add("minimum_free_disk_bytes_not_available");
        if (config.Instruments is null || config.Instruments.Count == 0 || config.Instruments.Any(x => !AllowedInstruments.Contains(x))) issues.Add("instrument_not_allowlisted");
        if (config.AllowedOutboundFixMsgTypes is null) issues.Add("allowed_outbound_fix_msg_types_required");
        else if (config.AllowedOutboundFixMsgTypes.Any(x => !LmaxMarketDataOnlyOutboundFixMessageGuard.AllowedOutboundMsgTypes.Contains(x))) issues.Add("outbound_fix_whitelist_contains_forbidden_or_unknown_type");
        if (FindForbiddenConfigShapeIssues(config, configDocument).Count > 0) issues.Add("config_contains_order_account_or_db_shape");
        if (Directory.Exists(config.OutputRoot) && outputRootMustBeEmpty && Directory.EnumerateFileSystemEntries(config.OutputRoot).Any()) issues.Add("output_root_not_empty");
        return new LmaxMarketDataOnlyPreflightReport(issues.Count == 0 ? "GO_M2C1B_PREFLIGHT_READY" : "NO_GO_M2C1B", issues, config.ConfigHash, networkDisabled, noOrderEntry, noAccountApi, noDb);
    }

    public static IReadOnlyList<string> FindForbiddenConfigShapeIssues(LmaxMarketDataOnlyPreflightConfig config, JsonElement? configDocument = null)
    {
        var issues = new List<string>();
        if (!string.Equals(config.CredentialScope, "MARKET_DATA_ONLY", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(config.MarketDataCredentialReference))
        {
            issues.Add("market_data_credential_reference_requires_market_data_only_scope");
        }

        if (configDocument.HasValue)
        {
            InspectConfigElement(configDocument.Value, "$", issues);
        }

        return issues;
    }

    private static bool HasRequiredFreeDisk(string outputRoot, long minimumFreeDiskBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(outputRoot));
            if (string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace >= minimumFreeDiskBytes;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void InspectConfigElement(JsonElement element, string path, List<string> issues)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{path}.{property.Name}";
                    if (IsForbiddenConfigPropertyName(property.Name))
                    {
                        issues.Add($"forbidden_config_field:{propertyPath}");
                    }

                    InspectConfigElement(property.Value, propertyPath, issues);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    InspectConfigElement(item, $"{path}[{index++}]", issues);
                }

                break;
        }
    }

    private static bool IsForbiddenConfigPropertyName(string propertyName)
    {
        var normalized = NormalizeConfigPropertyName(propertyName);
        return normalized is "orderentrysessionalias"
            or "orderentrycredentials"
            or "accountapi"
            or "dbconnection"
            or "liveorder"
            or "sendorder"
            || normalized.Contains("password", StringComparison.Ordinal);
    }

    private static string NormalizeConfigPropertyName(string propertyName)
        => new(propertyName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
