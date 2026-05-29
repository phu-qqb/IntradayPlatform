using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlySocketPrototypeStatus
{
    Blocked,
    BlockedMissingCredentials,
    BlockedSafetyGate,
    BlockedInvalidEnvironment,
    BlockedUnsafeVenue,
    BlockedOrderSubmissionFlag,
    Completed,
    CompletedWithWarnings,
    FailedSafe,
    FailedSafeConnectionError,
    FailedSafeLogonRejected,
    FailedSafeLogonLogoutReceived,
    FailedSafeLogonRejectReceived,
    FailedSafeLogonTimeout,
    FailedSafeLogonProfileMismatchSuspected,
    FailedSafeLogonTargetCompIdSuspected,
    FailedSafeLogonSenderCompIdSuspected,
    FailedSafeLogonCredentialsSuspected,
    FailedSafeLogonUnknown,
    FailedSafeSnapshotTimeout,
    FailedSafeMarketDataRequestRejected,
    FailedSafeMarketDataRequestRejectedValueOutOfRange263,
    FailedSafeMarketDataRequestRejectedUnknownTag55,
    FailedSafeMarketDataRequestRejectedGroupMismatch146,
    FailedSafeKnownRejectedRequestProfile,
    FailedSafeMarketDataRequestRejectedOther,
    FailedSafeBusinessReject,
    FailedSafeSessionReject,
    FailedSafeSymbolEncodingRejected,
    FailedSafeNoMarketDataEntries,
    FailedSafeUnexpectedLogout,
    CompletedWithEmptyBook,
    FailedSafeLogoutError,
    FailedSafeMaxRuntimeExceeded,
    FailedSafeMaxEventsExceeded
}

public enum LmaxReadOnlyMarketDataRequestMode
{
    SnapshotPlusUpdates,
    SnapshotOnly,
    AutoSequence
}

public enum LmaxReadOnlyMarketDataSymbolEncodingMode
{
    SecurityIdOnly,
    SecurityIdAndSymbolWithIdSource,
    SecurityIdAndSymbolNoIdSource,
    SlashSymbol,
    InternalSymbol,
    Auto
}

public sealed record LmaxReadOnlyMarketDataRequestProfile(
    LmaxReadOnlyMarketDataRequestMode RequestMode,
    LmaxReadOnlyMarketDataSymbolEncodingMode SymbolEncodingMode,
    bool KnownRejectedByLmaxDemo,
    string? RejectionReason,
    bool SafeToAttempt,
    bool RequiresUnsubscribeAfterSnapshot,
    string ExpectedSubscriptionRequestType,
    IReadOnlyList<string> SanitizedFieldSummary);

public static class LmaxReadOnlyMarketDataRequestCompatibility
{
    public static LmaxReadOnlyMarketDataRequestProfile CreateProfile(LmaxReadOnlySocketPrototypeOptions options)
    {
        var requestMode = options.RequestMode == LmaxReadOnlyMarketDataRequestMode.AutoSequence
            ? LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates
            : options.RequestMode;
        var symbolMode = options.SymbolEncodingMode == LmaxReadOnlyMarketDataSymbolEncodingMode.Auto
            ? LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly
            : options.SymbolEncodingMode;

        var knownRejected = false;
        var rejectionReason = (string?)null;
        if (requestMode == LmaxReadOnlyMarketDataRequestMode.SnapshotOnly)
        {
            knownRejected = true;
            rejectionReason = "LMAX Demo rejected SnapshotOnly SubscriptionRequestType 263=0 with ValueOutOfRange.";
        }
        else if (symbolMode == LmaxReadOnlyMarketDataSymbolEncodingMode.InternalSymbol)
        {
            knownRejected = true;
            rejectionReason = "LMAX Demo rejected InternalSymbol 55=EURUSD with repeating-group mismatch around tag 146.";
        }
        else if (symbolMode is LmaxReadOnlyMarketDataSymbolEncodingMode.SlashSymbol
                 or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource
                 or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource)
        {
            knownRejected = true;
            rejectionReason = "LMAX Demo has rejected request shapes containing tag 55 in some market-data instrument encodings.";
        }

        return new(
            requestMode,
            symbolMode,
            knownRejected,
            rejectionReason,
            SafeToAttempt: !knownRejected || options.AllowKnownRejectedDiagnostics,
            RequiresUnsubscribeAfterSnapshot: requestMode == LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates,
            ExpectedSubscriptionRequestType: requestMode == LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates ? "1" : "0",
            SanitizedFieldSummary: BuildFieldSummary(options, requestMode, symbolMode));
    }

    private static IReadOnlyList<string> BuildFieldSummary(
        LmaxReadOnlySocketPrototypeOptions options,
        LmaxReadOnlyMarketDataRequestMode requestMode,
        LmaxReadOnlyMarketDataSymbolEncodingMode symbolMode)
    {
        var fields = new List<string>
        {
            "262 present",
            "263=" + (requestMode == LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates ? "1" : "0"),
            "264=" + options.MarketDepth.ToString(CultureInfo.InvariantCulture),
            "267=2",
            "269=0,1",
            "146=1"
        };

        if (symbolMode is LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly
            or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource
            or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource)
        {
            fields.Add("48 present");
        }

        if (symbolMode is LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly
            or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource)
        {
            fields.Add("22=8");
        }

        if (symbolMode is LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource
            or LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource
            or LmaxReadOnlyMarketDataSymbolEncodingMode.SlashSymbol
            or LmaxReadOnlyMarketDataSymbolEncodingMode.InternalSymbol)
        {
            fields.Add("55 present");
        }
        else
        {
            fields.Add("55 omitted");
        }

        return fields;
    }
}

public enum LmaxReadOnlySocketPrototypeRetryRecommendation
{
    NoRetry,
    FixCredentialsThenRetry,
    ReviewFailureThenRetry,
    DoNotRetry
}

public sealed record LmaxReadOnlySocketPrototypeOptions
{
    public const int SafeMaxRuntimeSeconds = 30;
    public const int SafeMaxEventsPerRun = 25;

    public bool Enabled { get; init; }
    public LmaxReadOnlyRuntimeImplementationMode ImplementationMode { get; init; } = LmaxReadOnlyRuntimeImplementationMode.DesignOnly;
    public LmaxReadOnlyRuntimeActivationLevel ActivationLevel { get; init; } = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton;
    public string EnvironmentName { get; init; } = "Demo";
    public string VenueProfileName { get; init; } = LmaxReadOnlyVenueProfileName.DemoLondon.Value;
    public string CredentialProfileName { get; init; } = "LmaxDemoReadOnlyProfile";
    public string? Reason { get; init; }
    public string OperatorId { get; init; } = "local-operator";
    public bool ConfirmDemoReadOnly { get; init; }
    public bool AllowExternalConnections { get; init; }
    public bool AllowCredentialUse { get; init; }
    public bool AllowOrderSubmission { get; init; }
    public bool PersistToTradingTables { get; init; }
    public bool SchedulerEnabled { get; init; }
    public bool SubmitToShadowReplay { get; init; }
    public bool DryRun { get; init; } = true;
    public bool ResolveCredentialAvailabilityOnly { get; init; }
    public int MaxRuntimeSeconds { get; init; } = 15;
    public int MaxEventsPerRun { get; init; } = 5;
    public int MaxWaitSeconds { get; init; } = 15;
    public string Instrument { get; init; } = "EURUSD";
    public string SecurityId { get; init; } = "4001";
    public string SlashSymbol { get; init; } = "EUR/USD";
    public LmaxReadOnlyMarketDataRequestMode RequestMode { get; init; } = LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates;
    public LmaxReadOnlyMarketDataSymbolEncodingMode SymbolEncodingMode { get; init; } = LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly;
    public bool SkipKnownRejectedProfiles { get; init; } = true;
    public bool AllowKnownRejectedDiagnostics { get; init; }
    public int MarketDepth { get; init; } = 1;
}

public sealed record LmaxReadOnlySocketPrototypeSafetyReport(
    bool Passed,
    string BlockedReason,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates)
{
    public IReadOnlyList<string> FailedGateNames => Gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
}

public sealed record LmaxReadOnlySocketPrototypeRetryPolicy(
    bool RetryEnabled,
    bool RetryAllowed,
    int MaxAttempts,
    string RetryReason,
    LmaxReadOnlySocketPrototypeRetryRecommendation Recommendation,
    string FutureRetryClassification)
{
    public static LmaxReadOnlySocketPrototypeRetryPolicy ForStatus(LmaxReadOnlySocketPrototypeStatus status)
    {
        var recommendation = status switch
        {
            LmaxReadOnlySocketPrototypeStatus.BlockedMissingCredentials => LmaxReadOnlySocketPrototypeRetryRecommendation.FixCredentialsThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeConnectionError => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejectReceived => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonProfileMismatchSuspected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTargetCompIdSuspected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonSenderCompIdSuspected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonCredentialsSuspected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonUnknown => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedValueOutOfRange263 => LmaxReadOnlySocketPrototypeRetryRecommendation.DoNotRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedUnknownTag55 => LmaxReadOnlySocketPrototypeRetryRecommendation.DoNotRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedGroupMismatch146 => LmaxReadOnlySocketPrototypeRetryRecommendation.DoNotRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeKnownRejectedRequestProfile => LmaxReadOnlySocketPrototypeRetryRecommendation.DoNotRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedOther => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeSessionReject => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeSymbolEncodingRejected => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout => LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry,
            LmaxReadOnlySocketPrototypeStatus.Completed => LmaxReadOnlySocketPrototypeRetryRecommendation.NoRetry,
            LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings => LmaxReadOnlySocketPrototypeRetryRecommendation.NoRetry,
            LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook => LmaxReadOnlySocketPrototypeRetryRecommendation.NoRetry,
            _ => LmaxReadOnlySocketPrototypeRetryRecommendation.DoNotRetry
        };

        var reason = recommendation switch
        {
            LmaxReadOnlySocketPrototypeRetryRecommendation.FixCredentialsThenRetry => "Fix missing credential labels, rerun credential check, then manually retry if still appropriate.",
            LmaxReadOnlySocketPrototypeRetryRecommendation.ReviewFailureThenRetry => "Review sanitized failure details and rollback checks before any future manual retry.",
            LmaxReadOnlySocketPrototypeRetryRecommendation.NoRetry => "No automatic retry is needed.",
            _ => "Do not retry until the blocking safety condition is understood."
        };

        return new(
            RetryEnabled: false,
            RetryAllowed: false,
            MaxAttempts: 1,
            reason,
            recommendation,
            "Phase5E_NoAutomaticExternalRetry");
    }
}

public sealed record LmaxReadOnlyMarketDataSnapshotRequestDiagnostics(
    string DiagnosticVersion,
    string RequestId,
    string RequestIdHash,
    string Instrument,
    string SecurityId,
    LmaxReadOnlyMarketDataRequestMode RequestMode,
    LmaxReadOnlyMarketDataSymbolEncodingMode SymbolEncodingMode,
    string SecurityIdSource,
    int MarketDepth,
    string SubscriptionRequestType,
    bool KnownRejectedByLmaxDemo,
    string? RejectionReason,
    bool SafeToAttempt,
    bool RequiresUnsubscribeAfterSnapshot,
    IReadOnlyList<string> SanitizedFieldSummary,
    IReadOnlyList<string> MdEntryTypes,
    DateTimeOffset? RequestSentAtUtc,
    DateTimeOffset? FirstResponseAtUtc,
    DateTimeOffset? TimeoutAtUtc,
    long? WaitDurationMs);

public sealed record LmaxReadOnlyMarketDataSnapshotMessageCounters(
    int Logon,
    int MarketDataRequest,
    int MarketDataSnapshot,
    int MarketDataRequestReject,
    int BusinessMessageReject,
    int Reject,
    int Logout,
    int Heartbeat,
    int TestRequest,
    int Other);

public sealed record LmaxReadOnlyMarketDataSnapshotDiagnostics(
    string DiagnosticVersion,
    LmaxReadOnlyMarketDataSnapshotRequestDiagnostics Request,
    LmaxReadOnlyMarketDataSnapshotMessageCounters MessageCounters,
    LmaxReadOnlySocketPrototypeStatus ResponseClassification,
    IReadOnlyList<string> SessionWarnings,
    IReadOnlyList<string> SessionErrors);

public sealed record LmaxReadOnlyFixSessionProfileComparison(
    string RuntimeProfileLabel,
    string LabProfileLabel,
    bool SameBeginString,
    bool SameHeartbeatInterval,
    bool SameEncryptMethod,
    bool SameResetSeqNumFlag,
    bool SameSenderCompIdSourceLabel,
    bool SameTargetCompIdSourceLabel,
    bool SameCredentialProfileName,
    bool SameConnectionProfileLabel,
    bool SameTlsSetting,
    bool SamePortLabel,
    bool SenderCompIdMismatchSuspected,
    bool TargetCompIdMismatchSuspected,
    string Summary);

public sealed record LmaxReadOnlyFixLogonDiagnostics(
    string DiagnosticVersion,
    string ConnectionProfileLabel,
    string EnvironmentName,
    string VenueProfileName,
    string CredentialProfileName,
    bool TargetCompIdPresent,
    bool SenderCompIdPresent,
    bool UsernamePresent,
    bool PasswordPresent,
    string BeginString,
    int? SenderCompIdLength,
    int? TargetCompIdLength,
    int? UsernameLength,
    int? PasswordLength,
    string ResetSeqNumFlag,
    int EncryptMethod,
    int HeartbeatInterval,
    int MsgSeqNumSentForLogon,
    string? FirstInboundMsgType,
    string? FirstInboundLogoutText,
    string? FirstInboundRejectText,
    long? LogonWaitDurationMs,
    bool TlsConnected,
    bool TcpConnected,
    LmaxReadOnlyFixSessionProfileComparison ProfileComparison,
    string RedactionStatus);

public sealed record LmaxReadOnlySocketPrototypeResult(
    string RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    LmaxReadOnlySocketPrototypeStatus Status,
    string EnvironmentName,
    string VenueProfileName,
    string CredentialProfileName,
    string Reason,
    string OperatorId,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool CredentialValuesReturned,
    bool LogonAttempted,
    bool LogonSucceeded,
    bool SnapshotRequestAttempted,
    bool SnapshotReceived,
    bool LogoutAttempted,
    bool LogoutSucceeded,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    int EventCount,
    int MessageCount,
    int EntryCount,
    bool MarketDataSnapshotReceived,
    string Instrument,
    string SecurityId,
    decimal? BestBid,
    decimal? BestAsk,
    decimal? Mid,
    DateTimeOffset? SnapshotReceivedAtUtc,
    bool NoSensitiveContent,
    string RedactionStatus,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    LmaxReadOnlySocketPrototypeRetryPolicy RetryPolicy,
    LmaxReadOnlyMarketDataSnapshotDiagnostics Diagnostics,
    LmaxReadOnlyFixLogonDiagnostics LogonDiagnostics,
    LmaxReadOnlyCredentialAvailabilityResult? CredentialAvailability,
    IReadOnlyList<string> RollbackInstructions,
    LmaxReadOnlySocketPrototypeSafetyReport Safety);

public sealed record LmaxReadOnlyDemoMarketDataSocketAttemptResult(
    bool ExternalConnectionAttempted,
    bool LogonAttempted,
    bool LogonSucceeded,
    bool SnapshotRequestAttempted,
    bool SnapshotReceived,
    bool LogoutAttempted,
    bool LogoutSucceeded,
    int MessageCount,
    int EntryCount,
    decimal? BestBid,
    decimal? BestAsk,
    decimal? Mid,
    DateTimeOffset? SnapshotReceivedAtUtc,
    LmaxReadOnlyMarketDataSnapshotDiagnostics Diagnostics,
    LmaxReadOnlyFixLogonDiagnostics LogonDiagnostics,
    LmaxReadOnlySocketPrototypeStatus? FailureStatus,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public interface ILmaxReadOnlyDemoMarketDataSocketClient
{
    Task<LmaxReadOnlyDemoMarketDataSocketAttemptResult> RunSnapshotAsync(
        LmaxReadOnlySocketPrototypeOptions options,
        IReadOnlyDictionary<string, string> internalCredentialValues,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxReadOnlySocketPrototypeTransport
{
    private readonly ILmaxReadOnlyCredentialAvailabilityResolver _credentialAvailabilityResolver;
    private readonly Func<string, string?> _readCredentialValue;
    private readonly ILmaxReadOnlyDemoMarketDataSocketClient _socketClient;

    public LmaxReadOnlySocketPrototypeTransport(
        ILmaxReadOnlyCredentialAvailabilityResolver? credentialAvailabilityResolver = null,
        Func<string, string?>? readCredentialValue = null,
        ILmaxReadOnlyDemoMarketDataSocketClient? socketClient = null)
    {
        _readCredentialValue = readCredentialValue ?? Environment.GetEnvironmentVariable;
        _credentialAvailabilityResolver = credentialAvailabilityResolver
            ?? new LmaxReadOnlyCredentialProfileResolverEnvironment(_readCredentialValue);
        _socketClient = socketClient ?? new LmaxReadOnlyDemoMarketDataSocketClient();
    }

    public async Task<LmaxReadOnlySocketPrototypeResult> RunDemoSnapshotAsync(
        LmaxReadOnlySocketPrototypeOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var credentialAvailability = options.ResolveCredentialAvailabilityOnly
            ? _credentialAvailabilityResolver.CheckAvailability(CreateCredentialRequest(options))
            : null;
        var safety = EvaluateSafety(options, credentialAvailability);
        var warnings = new List<string>
        {
            "Phase 5D prototype path is manual-only, Demo-only, EURUSD market-data snapshot only, and isolated from API/Worker gateway registration."
        };

        if (!safety.Passed)
        {
            var completedAtUtc = DateTimeOffset.UtcNow;
            var blockedStatus = ClassifyBlockedStatus(safety.FailedGateNames);
            return CreateResult(
                options,
                startedAtUtc,
                completedAtUtc,
                blockedStatus,
                ExternalConnectionAttempted: false,
                LogonAttempted: false,
                LogonSucceeded: false,
                SnapshotRequestAttempted: false,
                SnapshotReceived: false,
                LogoutAttempted: false,
                LogoutSucceeded: false,
                MessageCount: 0,
                EntryCount: 0,
                BestBid: null,
                BestAsk: null,
                Mid: null,
                SnapshotReceivedAtUtc: null,
                CreateDiagnostics(options, blockedStatus, requestSentAtUtc: null, firstResponseAtUtc: null, timeoutAtUtc: null, waitDurationMs: null, messageCounters: null, warnings, safety.FailedGateNames),
                CreateLogonDiagnostics(options, internalCredentialValues: null, tcpConnected: false, tlsConnected: false, msgSeqNumSentForLogon: 1, firstInboundMsgType: null, firstInboundText: null, logonWaitDurationMs: null),
                warnings,
                safety.FailedGateNames,
                credentialAvailability,
                safety);
        }

        var values = ReadInternalCredentialValues();
        try
        {
            var attempt = await _socketClient.RunSnapshotAsync(options, values, cancellationToken);
            var completedAtUtc = DateTimeOffset.UtcNow;
            warnings.AddRange(attempt.Warnings.Select(x => LmaxReadOnlyFixMessageRedactor.Redact(x)));
            var errors = attempt.Errors.Select(x => LmaxReadOnlyFixMessageRedactor.Redact(x)).ToList();
            var status = ClassifyAttemptStatus(options, startedAtUtc, completedAtUtc, attempt, errors);

            return CreateResult(
                options,
                startedAtUtc,
                completedAtUtc,
                status,
                attempt.ExternalConnectionAttempted,
                attempt.LogonAttempted,
                attempt.LogonSucceeded,
                attempt.SnapshotRequestAttempted,
                attempt.SnapshotReceived,
                attempt.LogoutAttempted,
                attempt.LogoutSucceeded,
                attempt.MessageCount,
                attempt.EntryCount,
                attempt.BestBid,
                attempt.BestAsk,
                attempt.Mid,
                attempt.SnapshotReceivedAtUtc,
                attempt.Diagnostics,
                attempt.LogonDiagnostics,
                warnings,
                errors,
                credentialAvailability,
                safety);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or OperationCanceledException)
        {
            var completedAtUtc = DateTimeOffset.UtcNow;
            var status = ex is OperationCanceledException
                ? LmaxReadOnlySocketPrototypeStatus.FailedSafeMaxRuntimeExceeded
                : LmaxReadOnlySocketPrototypeStatus.FailedSafeConnectionError;
            return CreateResult(
                options,
                startedAtUtc,
                completedAtUtc,
                status,
                ExternalConnectionAttempted: true,
                LogonAttempted: true,
                LogonSucceeded: false,
                SnapshotRequestAttempted: false,
                SnapshotReceived: false,
                LogoutAttempted: false,
                LogoutSucceeded: false,
                MessageCount: 0,
                EntryCount: 0,
                BestBid: null,
                BestAsk: null,
                Mid: null,
                SnapshotReceivedAtUtc: null,
                CreateDiagnostics(options, status, requestSentAtUtc: null, firstResponseAtUtc: null, timeoutAtUtc: completedAtUtc, waitDurationMs: (long)(completedAtUtc - startedAtUtc).TotalMilliseconds, messageCounters: null, warnings, [$"Phase5D demo snapshot failed safe: {ex.GetType().Name}."]),
                CreateLogonDiagnostics(options, internalCredentialValues: null, tcpConnected: true, tlsConnected: false, msgSeqNumSentForLogon: 1, firstInboundMsgType: null, firstInboundText: null, logonWaitDurationMs: (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
                warnings,
                [$"Phase5D demo snapshot failed safe: {ex.GetType().Name}."],
                credentialAvailability,
                safety);
        }
    }

    private static LmaxReadOnlySocketPrototypeStatus ClassifyBlockedStatus(IReadOnlyList<string> failedGateNames)
    {
        if (failedGateNames.Contains("CredentialAvailabilityConfigured")
            && !failedGateNames.Contains("ResolveCredentialAvailabilityOnly"))
        {
            return LmaxReadOnlySocketPrototypeStatus.BlockedMissingCredentials;
        }

        if (failedGateNames.Contains("EnvironmentName"))
        {
            return LmaxReadOnlySocketPrototypeStatus.BlockedInvalidEnvironment;
        }

        if (failedGateNames.Contains("VenueProfileName") || failedGateNames.Contains("VenueProfileKnown"))
        {
            return LmaxReadOnlySocketPrototypeStatus.BlockedUnsafeVenue;
        }

        if (failedGateNames.Contains("OrderSubmissionForbidden"))
        {
            return LmaxReadOnlySocketPrototypeStatus.BlockedOrderSubmissionFlag;
        }

        if (failedGateNames.Contains("KnownRejectedRequestProfile"))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeKnownRejectedRequestProfile;
        }

        return LmaxReadOnlySocketPrototypeStatus.BlockedSafetyGate;
    }

    private static LmaxReadOnlySocketPrototypeStatus ClassifyAttemptStatus(
        LmaxReadOnlySocketPrototypeOptions options,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        LmaxReadOnlyDemoMarketDataSocketAttemptResult attempt,
        IReadOnlyList<string> errors)
    {
        if (attempt.FailureStatus is not null)
        {
            return attempt.FailureStatus.Value;
        }

        if ((completedAtUtc - startedAtUtc) > TimeSpan.FromSeconds(options.MaxRuntimeSeconds))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeMaxRuntimeExceeded;
        }

        if (attempt.MessageCount >= options.MaxEventsPerRun && !attempt.SnapshotReceived)
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeMaxEventsExceeded;
        }

        if (attempt.LogoutAttempted && !attempt.LogoutSucceeded)
        {
            return attempt.SnapshotReceived
                ? LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings
                : LmaxReadOnlySocketPrototypeStatus.FailedSafeLogoutError;
        }

        if (!attempt.LogonSucceeded && errors.Any(x => x.Contains("reject", StringComparison.OrdinalIgnoreCase)))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejected;
        }

        if (attempt.LogonAttempted && !attempt.LogonSucceeded)
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout;
        }

        if (attempt.SnapshotRequestAttempted && !attempt.SnapshotReceived)
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout;
        }

        if (attempt.SnapshotReceived && attempt.EntryCount == 0)
        {
            return LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook;
        }

        return attempt.SnapshotReceived
            ? errors.Count > 0 ? LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings : LmaxReadOnlySocketPrototypeStatus.Completed
            : LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings;
    }

    private static LmaxReadOnlyCredentialProfileRequest CreateCredentialRequest(LmaxReadOnlySocketPrototypeOptions options)
        => new(options.CredentialProfileName, options.EnvironmentName, options.VenueProfileName, options.Reason);

    private IReadOnlyDictionary<string, string> ReadInternalCredentialValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var label in LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels)
        {
            values[label] = _readCredentialValue(label) ?? string.Empty;
        }

        return values;
    }

    private static LmaxReadOnlySocketPrototypeResult CreateResult(
        LmaxReadOnlySocketPrototypeOptions options,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        LmaxReadOnlySocketPrototypeStatus status,
        bool ExternalConnectionAttempted,
        bool LogonAttempted,
        bool LogonSucceeded,
        bool SnapshotRequestAttempted,
        bool SnapshotReceived,
        bool LogoutAttempted,
        bool LogoutSucceeded,
        int MessageCount,
        int EntryCount,
        decimal? BestBid,
        decimal? BestAsk,
        decimal? Mid,
        DateTimeOffset? SnapshotReceivedAtUtc,
        LmaxReadOnlyMarketDataSnapshotDiagnostics diagnostics,
        LmaxReadOnlyFixLogonDiagnostics logonDiagnostics,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors,
        LmaxReadOnlyCredentialAvailabilityResult? credentialAvailability,
        LmaxReadOnlySocketPrototypeSafetyReport safety)
        => new(
            Guid.NewGuid().ToString("N"),
            startedAtUtc,
            completedAtUtc,
            status,
            options.EnvironmentName,
            options.VenueProfileName,
            options.CredentialProfileName,
            options.Reason ?? string.Empty,
            string.IsNullOrWhiteSpace(options.OperatorId) ? "local-operator" : options.OperatorId,
            ExternalConnectionAttempted,
            CredentialReadAttempted: credentialAvailability?.CredentialReadAttempted ?? false,
            CredentialValuesReturned: false,
            LogonAttempted,
            LogonSucceeded,
            SnapshotRequestAttempted,
            SnapshotReceived,
            LogoutAttempted,
            LogoutSucceeded,
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            SchedulerStarted: false,
            EventCount: MessageCount,
            MessageCount,
            EntryCount,
            MarketDataSnapshotReceived: SnapshotReceived,
            options.Instrument,
            options.SecurityId,
            BestBid,
            BestAsk,
            Mid,
            SnapshotReceivedAtUtc,
            NoSensitiveContent: true,
            RedactionStatus: "Redacted",
            warnings,
            errors,
            LmaxReadOnlySocketPrototypeRetryPolicy.ForStatus(status),
            diagnostics with { ResponseClassification = status },
            logonDiagnostics,
            credentialAvailability,
            RollbackInstructions(),
            safety);

    public static LmaxReadOnlyMarketDataSnapshotDiagnostics CreateDiagnostics(
        LmaxReadOnlySocketPrototypeOptions options,
        LmaxReadOnlySocketPrototypeStatus classification,
        DateTimeOffset? requestSentAtUtc,
        DateTimeOffset? firstResponseAtUtc,
        DateTimeOffset? timeoutAtUtc,
        long? waitDurationMs,
        LmaxReadOnlyMarketDataSnapshotMessageCounters? messageCounters,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        var profile = LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(options);
        var requestId = CreateRequestId(requestSentAtUtc ?? DateTimeOffset.UtcNow);
        return new(
            DiagnosticVersion: "phase5g-snapshot-diagnostics-v1",
            Request: new(
                DiagnosticVersion: "phase5g-snapshot-diagnostics-v1",
                RequestId: requestId,
                RequestIdHash: HashIdentifier(requestId),
                Instrument: options.Instrument,
                SecurityId: options.SecurityId,
                RequestMode: profile.RequestMode,
                SymbolEncodingMode: profile.SymbolEncodingMode,
                SecurityIdSource: "8",
                MarketDepth: options.MarketDepth,
                SubscriptionRequestType: profile.ExpectedSubscriptionRequestType == "1" ? "SnapshotPlusUpdates" : "SnapshotOnly",
                KnownRejectedByLmaxDemo: profile.KnownRejectedByLmaxDemo,
                RejectionReason: profile.RejectionReason,
                SafeToAttempt: profile.SafeToAttempt,
                RequiresUnsubscribeAfterSnapshot: profile.RequiresUnsubscribeAfterSnapshot,
                SanitizedFieldSummary: profile.SanitizedFieldSummary,
                MdEntryTypes: ["Bid", "Offer"],
                requestSentAtUtc,
                firstResponseAtUtc,
                timeoutAtUtc,
                waitDurationMs),
            MessageCounters: messageCounters ?? new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            ResponseClassification: classification,
            SessionWarnings: warnings.Select(x => LmaxReadOnlyFixMessageRedactor.Redact(x)).ToList(),
            SessionErrors: errors.Select(x => LmaxReadOnlyFixMessageRedactor.Redact(x)).ToList());
    }

    public static LmaxReadOnlyFixLogonDiagnostics CreateLogonDiagnostics(
        LmaxReadOnlySocketPrototypeOptions options,
        IReadOnlyDictionary<string, string>? internalCredentialValues,
        bool tcpConnected,
        bool tlsConnected,
        int msgSeqNumSentForLogon,
        string? firstInboundMsgType,
        string? firstInboundText,
        long? logonWaitDurationMs)
    {
        var sender = TryGetInternalCredentialValue(internalCredentialValues, "LMAX_DEMO_SENDER_COMP_ID");
        var target = TryGetInternalCredentialValue(internalCredentialValues, "LMAX_DEMO_TARGET_COMP_ID");
        var user = TryGetInternalCredentialValue(internalCredentialValues, "LMAX_DEMO_FIX_USERNAME");
        var pass = TryGetInternalCredentialValue(internalCredentialValues, "LMAX_DEMO_FIX_PASSWORD");
        var sanitizedText = LmaxReadOnlyFixMessageRedactor.Redact(firstInboundText, internalCredentialValues?.Values);
        var logoutText = firstInboundMsgType == "5" ? sanitizedText : null;
        var rejectText = firstInboundMsgType == "3" ? sanitizedText : null;

        return new(
            DiagnosticVersion: "phase5j-logon-diagnostics-v1",
            ConnectionProfileLabel: "LmaxDemoMarketDataTls",
            EnvironmentName: options.EnvironmentName,
            VenueProfileName: options.VenueProfileName,
            CredentialProfileName: options.CredentialProfileName,
            TargetCompIdPresent: !string.IsNullOrWhiteSpace(target),
            SenderCompIdPresent: !string.IsNullOrWhiteSpace(sender),
            UsernamePresent: !string.IsNullOrWhiteSpace(user),
            PasswordPresent: !string.IsNullOrWhiteSpace(pass),
            BeginString: "FIX.4.4",
            SenderCompIdLength: string.IsNullOrWhiteSpace(sender) ? null : sender.Length,
            TargetCompIdLength: string.IsNullOrWhiteSpace(target) ? null : target.Length,
            UsernameLength: string.IsNullOrWhiteSpace(user) ? null : user.Length,
            PasswordLength: string.IsNullOrWhiteSpace(pass) ? null : pass.Length,
            ResetSeqNumFlag: "Y",
            EncryptMethod: 0,
            HeartbeatInterval: 30,
            MsgSeqNumSentForLogon: msgSeqNumSentForLogon,
            FirstInboundMsgType: string.IsNullOrWhiteSpace(firstInboundMsgType) ? null : firstInboundMsgType,
            FirstInboundLogoutText: logoutText,
            FirstInboundRejectText: rejectText,
            LogonWaitDurationMs: logonWaitDurationMs,
            TlsConnected: tlsConnected,
            TcpConnected: tcpConnected,
            ProfileComparison: CreateSessionProfileComparison(options, sender, target),
            RedactionStatus: "Redacted");
    }

    public static LmaxReadOnlyFixSessionProfileComparison CreateSessionProfileComparison(
        LmaxReadOnlySocketPrototypeOptions options,
        string? senderCompId,
        string? targetCompId)
    {
        var senderPresent = !string.IsNullOrWhiteSpace(senderCompId);
        var targetPresent = !string.IsNullOrWhiteSpace(targetCompId);
        return new(
            RuntimeProfileLabel: "RuntimePhase5JDemoMarketData",
            LabProfileLabel: "ConnectivityLabDemoMarketData",
            SameBeginString: true,
            SameHeartbeatInterval: true,
            SameEncryptMethod: true,
            SameResetSeqNumFlag: true,
            SameSenderCompIdSourceLabel: senderPresent,
            SameTargetCompIdSourceLabel: targetPresent,
            SameCredentialProfileName: string.Equals(options.CredentialProfileName, "LmaxDemoReadOnlyProfile", StringComparison.OrdinalIgnoreCase),
            SameConnectionProfileLabel: true,
            SameTlsSetting: true,
            SamePortLabel: true,
            SenderCompIdMismatchSuspected: !senderPresent,
            TargetCompIdMismatchSuspected: !targetPresent,
            Summary: senderPresent && targetPresent
                ? "Runtime and Connectivity Lab profile labels are aligned on sanitized FIX session settings."
                : "Runtime profile is missing one or more comp-id source labels; compare local credential labels before another manual attempt.");
    }

    private static string? TryGetInternalCredentialValue(IReadOnlyDictionary<string, string>? values, string label)
        => values is not null && values.TryGetValue(label, out var value) ? value : null;

    private static string CreateRequestId(DateTimeOffset timestamp)
        => "QQRO" + timestamp.ToString("HHmmss", CultureInfo.InvariantCulture);

    private static string HashIdentifier(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    public static LmaxReadOnlySocketPrototypeSafetyReport EvaluateSafety(
        LmaxReadOnlySocketPrototypeOptions options,
        LmaxReadOnlyCredentialAvailabilityResult? credentialAvailability = null)
    {
        var venueValidation = new LmaxReadOnlyVenueProfileRegistryDisabled().Validate(options.VenueProfileName, options.EnvironmentName);
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>
        {
            Gate("Phase5BManualScriptOnly", true, "manual script only", "prototype is not registered in API/Worker DI"),
            Gate("Enabled", options.Enabled, "true", options.Enabled ? "enabled explicitly" : "disabled"),
            Gate("ImplementationMode", options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly, "FutureReadOnly", options.ImplementationMode.ToString()),
            Gate("ActivationLevel", options.ActivationLevel == LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit, "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit", options.ActivationLevel.ToString()),
            Gate("EnvironmentName", string.Equals(options.EnvironmentName, "Demo", StringComparison.OrdinalIgnoreCase), "Demo", options.EnvironmentName),
            Gate("VenueProfileName", IsAllowedDemoVenue(options.VenueProfileName), "DemoLondon or LmaxDemoReadOnly", options.VenueProfileName),
            Gate("VenueProfileKnown", venueValidation.IsKnown && !venueValidation.HasErrors, "known compatible demo label", venueValidation.Descriptor?.VenueProfileName ?? "(unknown)"),
            Gate("ConfirmDemoReadOnly", options.ConfirmDemoReadOnly, "true", options.ConfirmDemoReadOnly.ToString()),
            Gate("AllowExternalConnections", options.AllowExternalConnections, "true for this manual prototype only", options.AllowExternalConnections.ToString()),
            Gate("AllowCredentialUse", options.AllowCredentialUse, "true only after a safe credential resolver exists", options.AllowCredentialUse.ToString()),
            Gate("ResolveCredentialAvailabilityOnly", options.ResolveCredentialAvailabilityOnly, "true before Phase 5D socket attempt", options.ResolveCredentialAvailabilityOnly.ToString()),
            Gate("CredentialProfileName", !string.IsNullOrWhiteSpace(options.CredentialProfileName), "non-empty label", string.IsNullOrWhiteSpace(options.CredentialProfileName) ? "(missing)" : "label present"),
            Gate("CredentialAvailabilityConfigured", credentialAvailability?.IsConfigured == true, "all required environment labels present", credentialAvailability is null ? "not checked" : credentialAvailability.IsConfigured.ToString()),
            Gate("CredentialValuesReturned", credentialAvailability?.CredentialValuesReturned == false, "false", credentialAvailability is null ? "not checked" : credentialAvailability.CredentialValuesReturned.ToString()),
            Gate("SensitiveMaterialReturned", credentialAvailability?.SensitiveMaterialReturned == false, "false", credentialAvailability is null ? "not checked" : credentialAvailability.SensitiveMaterialReturned.ToString()),
            Gate("Phase5DManualScriptOnly", true, "manual script only", "not registered in API/Worker"),
            Gate("Phase5DMarketDataOnly", string.Equals(options.Instrument, "EURUSD", StringComparison.OrdinalIgnoreCase) && options.SecurityId == "4001", "EURUSD / 4001", $"{options.Instrument} / {options.SecurityId}"),
            Gate("OrderSubmissionForbidden", !options.AllowOrderSubmission, "false", options.AllowOrderSubmission.ToString()),
            Gate("PersistToTradingTables", !options.PersistToTradingTables, "false", options.PersistToTradingTables.ToString()),
            Gate("SchedulerEnabled", !options.SchedulerEnabled, "false", options.SchedulerEnabled.ToString()),
            Gate("SubmitToShadowReplay", !options.SubmitToShadowReplay, "false", options.SubmitToShadowReplay.ToString()),
            Gate("DryRun", options.DryRun, "true", options.DryRun.ToString()),
            Gate("ReasonRequired", !string.IsNullOrWhiteSpace(options.Reason), "non-empty reason", string.IsNullOrWhiteSpace(options.Reason) ? "(missing)" : "present"),
            Gate("OperatorId", !string.IsNullOrWhiteSpace(options.OperatorId), "non-empty operator id", string.IsNullOrWhiteSpace(options.OperatorId) ? "(missing)" : "present"),
            Gate("MaxRuntimeSeconds", options.MaxRuntimeSeconds > 0 && options.MaxRuntimeSeconds <= LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds, $"1..{LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds}", options.MaxRuntimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("MaxEventsPerRun", options.MaxEventsPerRun > 0 && options.MaxEventsPerRun <= LmaxReadOnlySocketPrototypeOptions.SafeMaxEventsPerRun, $"1..{LmaxReadOnlySocketPrototypeOptions.SafeMaxEventsPerRun}", options.MaxEventsPerRun.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("MaxWaitSeconds", options.MaxWaitSeconds > 0 && options.MaxWaitSeconds <= LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds, $"1..{LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds}", options.MaxWaitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("MarketDepth", options.MarketDepth == 1, "1", options.MarketDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("RequestMode", Enum.IsDefined(options.RequestMode), "defined read-only diagnostic request mode", options.RequestMode.ToString()),
            Gate("SymbolEncodingMode", Enum.IsDefined(options.SymbolEncodingMode), "defined read-only symbol encoding mode", options.SymbolEncodingMode.ToString()),
            Gate("KnownRejectedRequestProfile", !options.SkipKnownRejectedProfiles || !LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(options).KnownRejectedByLmaxDemo || options.AllowKnownRejectedDiagnostics, "not known rejected unless explicitly allowed", LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(options).RejectionReason ?? "not known rejected"),
            Gate("OrderCapableMessageTypes", true, "none", "No order-command message types are implemented"),
            Gate("ShadowReplaySubmitAttempted", true, "false", "false"),
            Gate("TradingMutationAttempted", true, "false", "false")
        };

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var blockedReason = failed.Count == 0
            ? "Phase 5D manual Demo read-only snapshot gates passed."
            : "Blocked by Phase 5D prototype safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlySocketPrototypeSafetyReport(failed.Count == 0, blockedReason, gates);
    }

    private static bool IsAllowedDemoVenue(string venueProfileName)
        => string.Equals(venueProfileName, LmaxReadOnlyVenueProfileName.DemoLondon.Value, StringComparison.OrdinalIgnoreCase)
           || string.Equals(venueProfileName, LmaxReadOnlyVenueProfileName.LegacyDemoReadOnly.Value, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> RollbackInstructions()
        =>
        [
            "Stop the local prototype process with Ctrl+C or close the terminal.",
            "Clear any Phase 5D prototype environment variables from the shell.",
            "Run the API through the default disabled startup path.",
            "Verify /health still reports FakeLmaxGateway and liveTradingEnabled=false.",
            "Run scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1.",
            "Run scripts/check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck if credential labels were missing.",
            "Do not proceed with another manual attempt if failure classification is unknown.",
            "Run scripts/smoke-lmax-readonly-runtime-external-preflight-local.ps1 and confirm mutation counts are unchanged."
        ];

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}

public sealed class LmaxReadOnlyDemoMarketDataSocketClient : ILmaxReadOnlyDemoMarketDataSocketClient
{
    private const string DemoMarketDataDnsName = "fix-marketdata.london-demo.lmax.com";
    private const int DemoMarketDataTlsNumber = 443;
    private const char Soh = '\u0001';

    public async Task<LmaxReadOnlyDemoMarketDataSocketAttemptResult> RunSnapshotAsync(
        LmaxReadOnlySocketPrototypeOptions options,
        IReadOnlyDictionary<string, string> internalCredentialValues,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var sequenceNumber = 1;
        var messageCount = 0;
        var logonCount = 0;
        var marketDataRequestCount = 0;
        var marketDataSnapshotCount = 0;
        var marketDataRejectCount = 0;
        var businessRejectCount = 0;
        var sessionRejectCount = 0;
        var logoutCount = 0;
        var heartbeatCount = 0;
        var testRequestCount = 0;
        var otherCount = 0;
        var logonSucceeded = false;
        var snapshotRequestAttempted = false;
        var snapshotReceived = false;
        var logoutAttempted = false;
        var logoutSucceeded = false;
        decimal? bestBid = null;
        decimal? bestAsk = null;
        decimal? mid = null;
        var entryCount = 0;
        DateTimeOffset? requestSentAtUtc = null;
        DateTimeOffset? firstResponseAtUtc = null;
        DateTimeOffset? snapshotReceivedAtUtc = null;
        LmaxReadOnlySocketPrototypeStatus? failureStatus = null;
        var tcpConnected = false;
        var tlsConnected = false;
        var logonStartedAtUtc = DateTimeOffset.UtcNow;
        string? firstInboundMsgType = null;
        string? firstInboundText = null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.MaxRuntimeSeconds, 1, LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds)));

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(DemoMarketDataDnsName, DemoMarketDataTlsNumber, timeout.Token);
        tcpConnected = true;
        await using var stream = await CreateTlsStreamAsync(tcp, timeout.Token);
        tlsConnected = true;

        var sender = internalCredentialValues["LMAX_DEMO_SENDER_COMP_ID"];
        var target = internalCredentialValues["LMAX_DEMO_TARGET_COMP_ID"];
        var logon = BuildMessage("A", sequenceNumber++, sender, target,
        [
            ("98", "0"),
            ("108", "30"),
            ("141", "Y"),
            ("553", internalCredentialValues["LMAX_DEMO_FIX_USERNAME"]),
            ("554", internalCredentialValues["LMAX_DEMO_FIX_PASSWORD"])
        ]);

        await WriteAsciiAsync(stream, logon, timeout.Token);
        logonStartedAtUtc = DateTimeOffset.UtcNow;
        var logonResponse = await ReadFixResponseAsync(stream, timeout.Token);
        messageCount++;
        firstInboundMsgType = GetTag(logonResponse, "35");
        firstInboundText = GetTag(logonResponse, "58");
        CountMessageType(firstInboundMsgType, ref logonCount, ref marketDataSnapshotCount, ref marketDataRejectCount, ref businessRejectCount, ref sessionRejectCount, ref logoutCount, ref heartbeatCount, ref testRequestCount, ref otherCount);
        logonSucceeded = ContainsTag(logonResponse, "35", "A");
        if (!logonSucceeded)
        {
            errors.Add("FIX logon was not confirmed before timeout or first session response.");
            var logonFailureStatus = ClassifyLogonFailure(firstInboundMsgType, logonResponse);
            if (firstInboundMsgType == "5")
            {
                errors.Add("FIX Logout was received before logon confirmation.");
            }

            if (firstInboundMsgType == "3")
            {
                errors.Add("FIX session Reject was received before logon confirmation.");
            }

            return new(true, true, false, false, false, false, false, messageCount, 0, null, null, null, null,
                LmaxReadOnlySocketPrototypeTransport.CreateDiagnostics(options, logonFailureStatus, requestSentAtUtc, firstResponseAtUtc, DateTimeOffset.UtcNow, null, CreateCounters(logonCount, marketDataRequestCount, marketDataSnapshotCount, marketDataRejectCount, businessRejectCount, sessionRejectCount, logoutCount, heartbeatCount, testRequestCount, otherCount), warnings, errors),
                LmaxReadOnlySocketPrototypeTransport.CreateLogonDiagnostics(options, internalCredentialValues, tcpConnected, tlsConnected, 1, firstInboundMsgType, firstInboundText, (long)(DateTimeOffset.UtcNow - logonStartedAtUtc).TotalMilliseconds),
                logonFailureStatus, warnings, errors);
        }

        var requestId = "QQRO" + DateTimeOffset.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
        var request = BuildMessage("V", sequenceNumber++, sender, target, BuildMarketDataRequestFields(options, requestId));
        await WriteAsciiAsync(stream, request, timeout.Token);
        marketDataRequestCount++;
        requestSentAtUtc = DateTimeOffset.UtcNow;
        snapshotRequestAttempted = true;

        var entries = new List<LmaxFixMarketDataEntry>();
        while (messageCount < options.MaxEventsPerRun)
        {
            var message = await ReadFixResponseAsync(stream, timeout.Token);
            if (string.IsNullOrWhiteSpace(message))
            {
                break;
            }

            messageCount++;
            var messageType = GetTag(message, "35");
            firstResponseAtUtc ??= DateTimeOffset.UtcNow;
            CountMessageType(messageType, ref logonCount, ref marketDataSnapshotCount, ref marketDataRejectCount, ref businessRejectCount, ref sessionRejectCount, ref logoutCount, ref heartbeatCount, ref testRequestCount, ref otherCount);
            if (messageType == "W")
            {
                snapshotReceived = true;
                entries.AddRange(ParseMarketDataEntries(message));
                entryCount = entries.Count;
                snapshotReceivedAtUtc = DateTimeOffset.UtcNow;
                if (entryCount == 0)
                {
                    warnings.Add("Market data snapshot was received with no entries.");
                    failureStatus = LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook;
                }
                break;
            }

            if (messageType == "Y")
            {
                errors.Add("Market data request was rejected by FIX session.");
                failureStatus = ClassifyRejectMessage(message);
                break;
            }

            if (messageType == "j")
            {
                errors.Add("Business message reject was received before market data snapshot.");
                failureStatus = LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject;
                break;
            }

            if (messageType == "3")
            {
                errors.Add("FIX session reject was received before market data snapshot.");
                failureStatus = ClassifyRejectMessage(message);
                break;
            }

            if (messageType == "5")
            {
                warnings.Add("FIX session returned Logout before snapshot was received.");
                failureStatus = LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout;
                break;
            }
        }

        if (entries.Count > 0)
        {
            (bestBid, bestAsk, mid) = ComputeTopOfBook(entries);
        }

        logoutAttempted = true;
        try
        {
            var logout = BuildMessage("5", sequenceNumber, sender, target, [("58", "QQ read-only demo snapshot complete")]);
            await WriteAsciiAsync(stream, logout, CancellationToken.None);
            logoutSucceeded = true;
        }
        catch (IOException)
        {
            warnings.Add("Logout send failed after read-only snapshot attempt.");
        }

        if (!snapshotReceived && errors.Count == 0)
        {
            warnings.Add("Timed out before a market data snapshot was received.");
            failureStatus ??= LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout;
        }

        failureStatus = snapshotReceived && entryCount > 0
            ? failureStatus
            : failureStatus ?? LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout;

        var completedAtUtc = DateTimeOffset.UtcNow;
        return new(true, true, logonSucceeded, snapshotRequestAttempted, snapshotReceived, logoutAttempted, logoutSucceeded, messageCount, entryCount, bestBid, bestAsk, mid, snapshotReceivedAtUtc,
            LmaxReadOnlySocketPrototypeTransport.CreateDiagnostics(options, failureStatus ?? LmaxReadOnlySocketPrototypeStatus.Completed, requestSentAtUtc, firstResponseAtUtc, snapshotReceived ? null : completedAtUtc, requestSentAtUtc is null ? null : (long)(completedAtUtc - requestSentAtUtc.Value).TotalMilliseconds, CreateCounters(logonCount, marketDataRequestCount, marketDataSnapshotCount, marketDataRejectCount, businessRejectCount, sessionRejectCount, logoutCount, heartbeatCount, testRequestCount, otherCount), warnings, errors),
            LmaxReadOnlySocketPrototypeTransport.CreateLogonDiagnostics(options, internalCredentialValues, tcpConnected, tlsConnected, 1, firstInboundMsgType, firstInboundText, (long)(completedAtUtc - logonStartedAtUtc).TotalMilliseconds),
            failureStatus, warnings, errors);
    }

    private static IReadOnlyList<(string Tag, string Value)> BuildMarketDataRequestFields(LmaxReadOnlySocketPrototypeOptions options, string requestId)
    {
        var fields = new List<(string Tag, string Value)>
        {
            ("262", requestId),
            ("263", LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(options).ExpectedSubscriptionRequestType),
            ("264", options.MarketDepth.ToString(CultureInfo.InvariantCulture)),
            ("267", "2"),
            ("269", "0"),
            ("269", "1"),
            ("146", "1")
        };

        var profile = LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(options);
        if (profile.SymbolEncodingMode == LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly)
        {
            fields.Add(("48", options.SecurityId));
            fields.Add(("22", "8"));
            return fields;
        }

        if (profile.SymbolEncodingMode == LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource)
        {
            fields.Add(("48", options.SecurityId));
            fields.Add(("22", "8"));
            fields.Add(("55", options.SlashSymbol));
            return fields;
        }

        if (profile.SymbolEncodingMode == LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdAndSymbolNoIdSource)
        {
            fields.Add(("48", options.SecurityId));
            fields.Add(("55", options.SlashSymbol));
            return fields;
        }

        if (profile.SymbolEncodingMode == LmaxReadOnlyMarketDataSymbolEncodingMode.SlashSymbol)
        {
            fields.Add(("55", options.SlashSymbol));
            return fields;
        }

        fields.Add(("55", options.Instrument));
        return fields;
    }

    private static LmaxReadOnlySocketPrototypeStatus ClassifyRejectMessage(string message)
    {
        if (message.Contains("263", StringComparison.OrdinalIgnoreCase)
            && message.Contains("ValueOutOfRange", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedValueOutOfRange263;
        }

        if (message.Contains("55", StringComparison.OrdinalIgnoreCase)
            && message.Contains("UnknownTag", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedUnknownTag55;
        }

        if (message.Contains("146", StringComparison.OrdinalIgnoreCase)
            && message.Contains("RepeatingGroupNumInGroupMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedGroupMismatch146;
        }

        if (message.Contains("55", StringComparison.OrdinalIgnoreCase)
            || message.Contains("48=", StringComparison.Ordinal)
            || message.Contains("22=", StringComparison.Ordinal))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeSymbolEncodingRejected;
        }

        return LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedOther;
    }

    private static LmaxReadOnlySocketPrototypeStatus ClassifyLogonFailure(string? messageType, string message)
    {
        if (messageType == "5")
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived;
        }

        if (messageType == "3")
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejectReceived;
        }

        if (message.Contains("TargetCompID", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TargetCompId", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTargetCompIdSuspected;
        }

        if (message.Contains("SenderCompID", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SenderCompId", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonSenderCompIdSuspected;
        }

        if (message.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("username", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonCredentialsSuspected;
        }

        return string.IsNullOrWhiteSpace(messageType)
            ? LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout
            : LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonUnknown;
    }

    private static LmaxReadOnlyMarketDataSnapshotMessageCounters CreateCounters(
        int logon,
        int marketDataRequest,
        int marketDataSnapshot,
        int marketDataReject,
        int businessReject,
        int sessionReject,
        int logout,
        int heartbeat,
        int testRequest,
        int other)
        => new(logon, marketDataRequest, marketDataSnapshot, marketDataReject, businessReject, sessionReject, logout, heartbeat, testRequest, other);

    private static void CountMessageType(
        string? messageType,
        ref int logon,
        ref int marketDataSnapshot,
        ref int marketDataReject,
        ref int businessReject,
        ref int sessionReject,
        ref int logout,
        ref int heartbeat,
        ref int testRequest,
        ref int other)
    {
        switch (messageType)
        {
            case "A":
                logon++;
                break;
            case "W":
                marketDataSnapshot++;
                break;
            case "Y":
                marketDataReject++;
                break;
            case "j":
                businessReject++;
                break;
            case "3":
                sessionReject++;
                break;
            case "5":
                logout++;
                break;
            case "0":
                heartbeat++;
                break;
            case "1":
                testRequest++;
                break;
            default:
                other++;
                break;
        }
    }

    private static async Task<SslStream> CreateTlsStreamAsync(TcpClient tcp, CancellationToken cancellationToken)
    {
        var stream = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = DemoMarketDataDnsName,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, cancellationToken);
        return stream;
    }

    private static async Task WriteAsciiAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadFixResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var memory = new MemoryStream();
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            memory.Write(buffer, 0, read);
            var text = Encoding.ASCII.GetString(memory.ToArray());
            if (TryGetCompleteFixMessage(text, out var message))
            {
                return message;
            }
        }

        return Encoding.ASCII.GetString(memory.ToArray());
    }

    private static bool TryGetCompleteFixMessage(string text, out string message)
    {
        message = string.Empty;
        var checksumIndex = text.IndexOf($"{Soh}10=", StringComparison.Ordinal);
        if (checksumIndex < 0)
        {
            return false;
        }

        var end = checksumIndex + 1 + "10=".Length + 3;
        if (text.Length <= end || text[end] != Soh)
        {
            return false;
        }

        message = text[..(end + 1)];
        return true;
    }

    private static string BuildMessage(string messageType, int sequenceNumber, string senderCompId, string targetCompId, IReadOnlyList<(string Tag, string Value)> fields)
    {
        var body = new StringBuilder();
        body.Append("35=").Append(messageType).Append(Soh);
        body.Append("34=").Append(sequenceNumber.ToString(CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("49=").Append(senderCompId).Append(Soh);
        body.Append("52=").Append(DateTimeOffset.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("56=").Append(targetCompId).Append(Soh);
        foreach (var (tag, value) in fields)
        {
            body.Append(tag).Append('=').Append(value).Append(Soh);
        }

        var head = $"8=FIX.4.4{Soh}9={Encoding.ASCII.GetByteCount(body.ToString())}{Soh}";
        var withoutChecksum = head + body;
        var checksum = Encoding.ASCII.GetBytes(withoutChecksum).Sum(x => x) % 256;
        return withoutChecksum + $"10={checksum.ToString("000", CultureInfo.InvariantCulture)}{Soh}";
    }

    private static bool ContainsTag(string message, string tag, string value)
        => message.Contains($"{Soh}{tag}={value}{Soh}", StringComparison.Ordinal)
           || message.StartsWith($"{tag}={value}{Soh}", StringComparison.Ordinal);

    private static string? GetTag(string message, string tag)
        => message.Split(Soh, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2 && x[0] == tag)
            .Select(x => x[1])
            .LastOrDefault();

    private static IReadOnlyList<LmaxFixMarketDataEntry> ParseMarketDataEntries(string message)
    {
        var fields = message.Split(Soh, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .Select(x => (Tag: x[0], Value: x[1]))
            .ToList();
        var mdReqId = fields.LastOrDefault(x => x.Tag == "262").Value;
        var symbol = fields.LastOrDefault(x => x.Tag == "55").Value;
        var securityId = fields.LastOrDefault(x => x.Tag == "48").Value;
        var entries = new List<LmaxFixMarketDataEntry>();
        LmaxFixMarketDataEntry? current = null;

        foreach (var (tag, value) in fields)
        {
            if (tag == "269")
            {
                if (current is not null)
                {
                    entries.Add(current);
                }

                current = new LmaxFixMarketDataEntry(value, null, null, null);
                _ = mdReqId;
                _ = symbol;
                _ = securityId;
                continue;
            }

            if (current is null)
            {
                continue;
            }

            current = tag switch
            {
                "270" => current with { Price = ParseDecimal(value) },
                "271" => current with { Size = ParseDecimal(value) },
                _ => current
            };
        }

        if (current is not null)
        {
            entries.Add(current);
        }

        return entries;
    }

    private static (decimal? BestBid, decimal? BestAsk, decimal? Mid) ComputeTopOfBook(IEnumerable<LmaxFixMarketDataEntry> entries)
    {
        var bestBid = entries.Where(x => x.EntryType == "0" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Max();
        var bestAsk = entries.Where(x => x.EntryType == "1" && x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Min();
        decimal? bid = bestBid == 0m ? null : bestBid;
        decimal? ask = bestAsk == 0m ? null : bestAsk;
        return (bid, ask, bid.HasValue && ask.HasValue ? (bid.Value + ask.Value) / 2m : null);
    }

    private static decimal? ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private sealed record LmaxFixMarketDataEntry(string EntryType, decimal? Price, decimal? Size, DateTimeOffset? SourceTimestampUtc);
}

public static class LmaxReadOnlyFixMessageRedactor
{
    private static readonly string[] SecretPatterns =
    [
        "(?i)(554=)[^\u0001|]+",
        "(?i)(553=)[^\u0001|]+",
        "(?i)(49=)[^\u0001|]+",
        "(?i)(56=)[^\u0001|]+",
        "(?i)(password\\s*[:=]\\s*)[^,;\\r\\n\\s]+",
        "(?i)(secret\\s*[:=]\\s*)[^,;\\r\\n\\s]+",
        "(?i)(token\\s*[:=]\\s*)[^,;\\r\\n\\s]+",
        "(?i)(apiKey\\s*[:=]\\s*)[^,;\\r\\n\\s]+",
        "(?i)(privateKey\\s*[:=]\\s*)[^,;\\r\\n\\s]+",
        "(?i)(bearer\\s+)[^,;\\r\\n\\s]+",
        "(?i)(authorization\\s*[:=]\\s*)[^,;\\r\\n]+"
    ];

    public static string Redact(string? value, IEnumerable<string>? additionalSensitiveValues = null)
    {
        var redacted = LmaxReadOnlyCredentialRedactionPolicy.Redact(value);
        foreach (var pattern in SecretPatterns)
        {
            redacted = System.Text.RegularExpressions.Regex.Replace(redacted, pattern, "$1[REDACTED]");
        }

        foreach (var sensitive in additionalSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitive))
            {
                redacted = redacted.Replace(sensitive, "[REDACTED]", StringComparison.Ordinal);
            }
        }

        return redacted;
    }
}

public static class LmaxReadOnlySocketPrototypeSanitizedArtifactWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string ToSanitizedJson(
        LmaxReadOnlySocketPrototypeResult result,
        IEnumerable<string>? additionalSensitiveValues = null)
    {
        var json = JsonSerializer.Serialize(result, Options);
        return LmaxReadOnlyFixMessageRedactor.Redact(json, additionalSensitiveValues);
    }

    public static string Write(
        LmaxReadOnlySocketPrototypeResult result,
        string artifactDirectory,
        IEnumerable<string>? additionalSensitiveValues = null)
    {
        Directory.CreateDirectory(artifactDirectory);
        var fileName = $"lmax-readonly-demo-snapshot-result-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(artifactDirectory, fileName);
        File.WriteAllText(path, ToSanitizedJson(result, additionalSensitiveValues), Encoding.UTF8);
        return path;
    }
}
