using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlySecurityListDiscoveryRequestProfile
{
    AllSecurities,
    ProductFx,
    SymbolExact,
    SecurityTypeFx,
    CandidateSymbolsOneByOne,
    MinimalRequest,
    LabCompatibleFallback,
    AutoSequence,
    AllInstruments,
    ForexOnly,
    SymbolFilter,
    CandidateSymbolsOnly
}

public enum LmaxReadOnlySecurityListDiscoveryStatus
{
    Completed,
    CompletedWithWarnings,
    FailedSafeSecurityListRequestRejected,
    FailedSafeSecurityListBusinessReject,
    FailedSafeSecurityListSessionReject,
    FailedSafeSecurityListUnsupportedRequestType,
    FailedSafeSecurityListUnsupportedSecurityRequestType,
    FailedSafeSecurityListUnsupportedSymbolFilter,
    FailedSafeSecurityListUnsupportedByVenue,
    FailedSafeSecurityListRequestTypeUnsupported,
    FailedSafeSecurityListProfileRejected,
    FailedSafeSecurityListNoSupportedProfiles,
    FailedSafeSecurityListUnknownReject,
    FailedSafeBusinessReject,
    FailedSafeSessionReject,
    FailedSafeSecurityListTimeout
}

public enum LmaxReadOnlySecurityListFallbackDecisionKind
{
    ContinueSecurityListDiagnostics,
    UseVendorSupportConfirmation,
    UseOfficialLmaxDocument,
    UseManualWebGuiInstrumentInfo,
    BlockedPendingEvidence
}

public sealed record LmaxReadOnlySecurityListRequestProfileDefinition(
    LmaxReadOnlySecurityListDiscoveryRequestProfile Profile,
    string ProfileName,
    string SecurityListRequestType,
    bool IncludesProductField,
    string? ProductFieldValue,
    bool IncludesSecurityTypeField,
    string? SecurityTypeFieldValue,
    bool IncludesSymbolField,
    bool CandidateSymbolFilter,
    bool KnownRejectedByLmaxDemo,
    string? RejectionReason,
    bool SafeToAttempt,
    string Notes);

public sealed record LmaxReadOnlySecurityListDiscoveryAttempt(
    string RequestProfile,
    string RequestIdHash,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? FirstResponseAtUtc,
    LmaxReadOnlySecurityListDiscoveryStatus Status,
    string? RejectMessageType,
    string? RejectTag,
    string? RejectText,
    int InstrumentCount,
    IReadOnlyList<LmaxReadOnlySecurityListDiscoveryCandidateMatch> CandidateMatches);

public sealed record LmaxReadOnlySecurityListFailureDiagnostics(
    string Status,
    string? RejectMessageType,
    string? RejectTag,
    string? RejectText,
    string? RequestProfile,
    string? SecurityListRequestType,
    bool LogonSucceeded,
    bool SecurityListRequestAttempted,
    bool LogoutSucceeded,
    LmaxReadOnlySecurityListDiscoveryStatus ResponseClassification,
    bool NoSensitiveContent,
    bool CredentialValuesReturned,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    bool IsApprovedForExternalRun);

public sealed record LmaxReadOnlySecurityListDiscoveryAttemptDiagnostics(
    string RequestProfile,
    string? FirstInboundMessageType,
    string? RejectMessageType,
    string? RejectTag,
    string? RejectText,
    string Classification,
    int InstrumentCount,
    int CandidateMatchCount);

public sealed record LmaxReadOnlySecurityListDiscoveryFallbackDecision(
    LmaxReadOnlySecurityListFallbackDecisionKind RecommendedDecision,
    string Reason,
    string FinalStatus,
    string? RequestProfile,
    IReadOnlyList<LmaxReadOnlySecurityListDiscoveryAttemptDiagnostics> Attempts,
    IReadOnlyList<string> AttemptedProfiles,
    bool AllProfilesFailedWithSameClass,
    bool LikelySecurityListUnsupportedByVenue,
    bool MissingRejectDiagnostics,
    int CandidateMatchCount,
    IReadOnlyList<string> UnmatchedCandidates,
    bool IsApprovedForExternalRun,
    bool ExternalRunAuthorized);

public sealed record LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidationResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    LmaxReadOnlySecurityListDiscoveryArtifactValidationResult ArtifactValidation,
    LmaxReadOnlySecurityListDiscoveryFallbackDecision FallbackDecision,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public sealed record LmaxReadOnlySecurityListDiscoveryArtifactValidationResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    LmaxReadOnlySecurityListFailureDiagnostics Diagnostics,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public sealed record LmaxReadOnlySecurityListDiscoveryOptions
{
    public string EnvironmentName { get; init; } = "Demo";
    public string CredentialProfileName { get; init; } = "LmaxDemoReadOnlyProfile";
    public LmaxReadOnlySecurityListDiscoveryRequestProfile RequestProfile { get; init; } = LmaxReadOnlySecurityListDiscoveryRequestProfile.AllInstruments;
    public string? SymbolFilter { get; init; }
    public int MaxWaitSeconds { get; init; } = 15;
    public int MaxMessages { get; init; } = 25;
    public int MaxInstruments { get; init; } = 500;
}

public sealed record LmaxReadOnlySecurityListDiscoveryInstrument(
    string? Symbol,
    string? SlashSymbol,
    string? SecurityId,
    string? SecurityIdSource,
    string? SecurityType,
    string? Currency,
    string? QuoteCurrency,
    string SourceMessageType);

public sealed record LmaxReadOnlySecurityListDiscoveryCandidateMatch(
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string? SecurityIdSource,
    string? SecurityType,
    string? Currency,
    string? QuoteCurrency,
    string SourceMessageType,
    bool IsApprovedForExternalRun);

public sealed record LmaxReadOnlySecurityListDiscoveryResult(
    string DiscoveryId,
    DateTimeOffset CreatedAtUtc,
    LmaxReadOnlySecurityListDiscoveryStatus Status,
    string EnvironmentName,
    string CredentialProfileName,
    bool ExternalConnectionAttempted,
    bool CredentialReadAttempted,
    bool CredentialValuesReturned,
    bool LogonAttempted,
    bool LogonSucceeded,
    bool SecurityListRequestAttempted,
    bool SecurityListReceived,
    bool LogoutAttempted,
    bool LogoutSucceeded,
    int TotalInstrumentCount,
    IReadOnlyList<LmaxReadOnlySecurityListDiscoveryCandidateMatch> CandidateMatches,
    IReadOnlyList<string> UnmatchedCandidates,
    IReadOnlyList<LmaxReadOnlySecurityListDiscoveryInstrument> Instruments,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool NoSensitiveContent,
    string RedactionStatus,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted);

public static class LmaxReadOnlySecurityListDiscovery
{
    public static readonly IReadOnlyList<(string Symbol, string SlashSymbol)> CandidateSymbols =
    [
        ("GBPUSD", "GBP/USD"),
        ("USDJPY", "USD/JPY"),
        ("EURGBP", "EUR/GBP"),
        ("AUDUSD", "AUD/USD")
    ];

    public static IReadOnlyList<LmaxReadOnlySecurityListRequestProfileDefinition> ProfileDefinitions { get; } =
    [
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.MinimalRequest,
            "MinimalRequest",
            "4",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: false,
            RejectionReason: null,
            SafeToAttempt: true,
            Notes: "Minimal all-securities request using SecurityListRequestType=4 only."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.ProductFx,
            "ProductFx",
            "1",
            IncludesProductField: true,
            ProductFieldValue: "4",
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: false,
            RejectionReason: null,
            SafeToAttempt: true,
            Notes: "Requests FX/product-filtered securities when supported by venue."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.SecurityTypeFx,
            "SecurityTypeFx",
            "1",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: true,
            SecurityTypeFieldValue: "FOR",
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: false,
            RejectionReason: null,
            SafeToAttempt: true,
            Notes: "Requests FX securities using SecurityType=FOR when supported."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolExact,
            "SymbolExact",
            "0",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: true,
            CandidateSymbolFilter: true,
            KnownRejectedByLmaxDemo: true,
            RejectionReason: "The first SecurityList attempt did not establish that symbol-filtered SecurityListRequest is accepted by LMAX Demo.",
            SafeToAttempt: false,
            Notes: "Single-symbol exact lookup; requires explicit known-rejected diagnostics override until proven safe."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.CandidateSymbolsOneByOne,
            "CandidateSymbolsOneByOne",
            "0",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: true,
            CandidateSymbolFilter: true,
            KnownRejectedByLmaxDemo: true,
            RejectionReason: "Candidate symbol sequence may be unsupported if symbol-filtered SecurityListRequest is rejected.",
            SafeToAttempt: false,
            Notes: "Attempts candidate symbols one by one only when known-rejected diagnostics are allowed."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.LabCompatibleFallback,
            "LabCompatibleFallback",
            "4",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: false,
            RejectionReason: null,
            SafeToAttempt: true,
            Notes: "Conservative fallback aligned with the connectivity-lab all-securities shape."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.AllSecurities,
            "AllSecurities",
            "4",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: true,
            RejectionReason: "The first Demo SecurityList discovery attempt using the old AllInstruments profile failed safely.",
            SafeToAttempt: false,
            Notes: "Historical profile retained for diagnostics; use MinimalRequest or LabCompatibleFallback by default."),
        new(
            LmaxReadOnlySecurityListDiscoveryRequestProfile.AllInstruments,
            "AllInstruments",
            "4",
            IncludesProductField: false,
            ProductFieldValue: null,
            IncludesSecurityTypeField: false,
            SecurityTypeFieldValue: null,
            IncludesSymbolField: false,
            CandidateSymbolFilter: false,
            KnownRejectedByLmaxDemo: true,
            RejectionReason: "Deprecated Phase 6I profile failed safely in Demo.",
            SafeToAttempt: false,
            Notes: "Backward-compatible alias for the rejected first profile.")
    ];

    public static IReadOnlyList<LmaxReadOnlySecurityListRequestProfileDefinition> GetAutoSequenceProfiles(bool allowKnownRejectedDiagnostics)
        => ProfileDefinitions
            .Where(x => x.Profile is not LmaxReadOnlySecurityListDiscoveryRequestProfile.AutoSequence)
            .Where(x => x.SafeToAttempt || allowKnownRejectedDiagnostics)
            .Where(x => x.Profile is not LmaxReadOnlySecurityListDiscoveryRequestProfile.AllInstruments)
            .ToArray();

    public static IReadOnlyList<(string Tag, string Value)> BuildSecurityListRequestFields(
        LmaxReadOnlySecurityListDiscoveryOptions options,
        string requestId)
    {
        var requestType = options.RequestProfile switch
        {
            LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolFilter or LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolExact or LmaxReadOnlySecurityListDiscoveryRequestProfile.CandidateSymbolsOneByOne => "0",
            LmaxReadOnlySecurityListDiscoveryRequestProfile.ForexOnly or LmaxReadOnlySecurityListDiscoveryRequestProfile.ProductFx or LmaxReadOnlySecurityListDiscoveryRequestProfile.SecurityTypeFx => "1",
            _ => "4"
        };
        var fields = new List<(string Tag, string Value)>
        {
            ("320", requestId),
            ("559", requestType)
        };

        if (options.RequestProfile is LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolFilter or LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolExact
            && !string.IsNullOrWhiteSpace(options.SymbolFilter))
        {
            fields.Add(("55", options.SymbolFilter));
        }

        if (options.RequestProfile is LmaxReadOnlySecurityListDiscoveryRequestProfile.ForexOnly or LmaxReadOnlySecurityListDiscoveryRequestProfile.SecurityTypeFx)
        {
            fields.Add(("167", "FOR"));
        }

        if (options.RequestProfile == LmaxReadOnlySecurityListDiscoveryRequestProfile.ProductFx)
        {
            fields.Add(("460", "4"));
        }

        return fields;
    }

    public static LmaxReadOnlySecurityListDiscoveryStatus ClassifyResponse(
        string? messageType,
        bool timedOut)
        => messageType switch
        {
            "y" => LmaxReadOnlySecurityListDiscoveryStatus.Completed,
            "j" => LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListBusinessReject,
            "3" => LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListSessionReject,
            "5" => LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestRejected,
            _ => timedOut
                ? LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListTimeout
                : LmaxReadOnlySecurityListDiscoveryStatus.CompletedWithWarnings
        };

    public static LmaxReadOnlySecurityListDiscoveryStatus ClassifyReject(
        string? messageType,
        string? rejectTag,
        string? rejectText,
        bool timedOut)
    {
        if (timedOut)
        {
            return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListTimeout;
        }

        var text = rejectText ?? string.Empty;
        if (messageType == "j")
        {
            return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListBusinessReject;
        }

        if (messageType == "3")
        {
            if (rejectTag == "559" || text.Contains("SecurityListRequestType", StringComparison.OrdinalIgnoreCase))
            {
                return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestTypeUnsupported;
            }

            if (rejectTag == "55" || text.Contains("symbol", StringComparison.OrdinalIgnoreCase))
            {
                return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedSymbolFilter;
            }

            if (text.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
            {
                return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedByVenue;
            }

            return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListSessionReject;
        }

        if (messageType == "5")
        {
            return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestRejected;
        }

        return LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnknownReject;
    }

    public static IReadOnlyList<LmaxReadOnlySecurityListDiscoveryInstrument> ParseMessages(
        IEnumerable<string> fixMessages,
        int maxInstruments)
    {
        var instruments = new List<LmaxReadOnlySecurityListDiscoveryInstrument>();
        foreach (var message in fixMessages)
        {
            var messageType = GetTag(message, "35");
            if (messageType is not ("y" or "d"))
            {
                continue;
            }

            instruments.AddRange(ParseInstrumentMessage(message, messageType));
            if (instruments.Count >= maxInstruments)
            {
                return instruments.Take(maxInstruments).ToArray();
            }
        }

        return instruments;
    }

    public static LmaxReadOnlySecurityListDiscoveryResult CreateResult(
        LmaxReadOnlySecurityListDiscoveryStatus status,
        LmaxReadOnlySecurityListDiscoveryOptions options,
        IEnumerable<string> fixMessages,
        bool externalConnectionAttempted,
        bool credentialReadAttempted,
        bool logonAttempted,
        bool logonSucceeded,
        bool securityListRequestAttempted,
        bool securityListReceived,
        bool logoutAttempted,
        bool logoutSucceeded,
        IEnumerable<string>? warnings = null,
        IEnumerable<string>? errors = null)
    {
        var instruments = ParseMessages(fixMessages, options.MaxInstruments);
        var matches = MatchCandidates(instruments);
        var unmatched = CandidateSymbols
            .Where(x => !matches.Any(match => match.Symbol.Equals(x.Symbol, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Symbol)
            .ToArray();
        var conflictWarnings = FindConflicts(matches).Select(x => $"Conflicting SecurityID entries detected for {x}.").ToArray();
        var allWarnings = warnings?.Concat(conflictWarnings).ToArray() ?? conflictWarnings;
        var effectiveStatus = status == LmaxReadOnlySecurityListDiscoveryStatus.Completed && (unmatched.Length > 0 || conflictWarnings.Length > 0)
            ? LmaxReadOnlySecurityListDiscoveryStatus.CompletedWithWarnings
            : status;

        return new(
            DiscoveryId: $"lmax-securitylist-discovery-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Status: effectiveStatus,
            EnvironmentName: options.EnvironmentName,
            CredentialProfileName: options.CredentialProfileName,
            ExternalConnectionAttempted: externalConnectionAttempted,
            CredentialReadAttempted: credentialReadAttempted,
            CredentialValuesReturned: false,
            LogonAttempted: logonAttempted,
            LogonSucceeded: logonSucceeded,
            SecurityListRequestAttempted: securityListRequestAttempted,
            SecurityListReceived: securityListReceived,
            LogoutAttempted: logoutAttempted,
            LogoutSucceeded: logoutSucceeded,
            TotalInstrumentCount: instruments.Count,
            CandidateMatches: matches,
            UnmatchedCandidates: unmatched,
            Instruments: instruments,
            Warnings: allWarnings,
            Errors: errors?.ToArray() ?? [],
            NoSensitiveContent: true,
            RedactionStatus: "Redacted",
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            SchedulerStarted: false);
    }

    public static IReadOnlyList<LmaxReadOnlySecurityListDiscoveryCandidateMatch> MatchCandidates(
        IEnumerable<LmaxReadOnlySecurityListDiscoveryInstrument> instruments)
    {
        var matches = new List<LmaxReadOnlySecurityListDiscoveryCandidateMatch>();
        foreach (var candidate in CandidateSymbols)
        {
            var candidateMatches = instruments
                .Where(x => IsCandidateMatch(x, candidate.Symbol, candidate.SlashSymbol)
                            && !string.IsNullOrWhiteSpace(x.SecurityId))
                .ToArray();

            foreach (var match in candidateMatches)
            {
                matches.Add(new(
                    candidate.Symbol,
                    candidate.SlashSymbol,
                    match.SecurityId!,
                    match.SecurityIdSource,
                    match.SecurityType,
                    match.Currency,
                    match.QuoteCurrency,
                    match.SourceMessageType,
                    IsApprovedForExternalRun: false));
            }
        }

        return matches;
    }

    public static IReadOnlyList<string> FindConflicts(
        IEnumerable<LmaxReadOnlySecurityListDiscoveryCandidateMatch> matches)
        => matches
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(x => x.SecurityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(x => x.Key)
            .ToArray();

    private static IEnumerable<LmaxReadOnlySecurityListDiscoveryInstrument> ParseInstrumentMessage(
        string message,
        string messageType)
    {
        var fields = ParseFields(message);
        var current = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (tag, value) in fields)
        {
            if (tag == "55" && (current.ContainsKey("55") || current.ContainsKey("48")))
            {
                yield return ToInstrument(current, messageType);
                current = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (tag is "55" or "48" or "22" or "167" or "15" or "120")
            {
                current[tag] = value;
            }
        }

        if (current.Count > 0)
        {
            yield return ToInstrument(current, messageType);
        }
    }

    private static LmaxReadOnlySecurityListDiscoveryInstrument ToInstrument(
        IReadOnlyDictionary<string, string> fields,
        string messageType)
    {
        fields.TryGetValue("55", out var symbol);
        fields.TryGetValue("48", out var securityId);
        fields.TryGetValue("22", out var securityIdSource);
        fields.TryGetValue("167", out var securityType);
        fields.TryGetValue("15", out var currency);
        fields.TryGetValue("120", out var quoteCurrency);
        return new(
            Symbol: NormalizeSymbol(symbol),
            SlashSymbol: NormalizeSlashSymbol(symbol),
            SecurityId: securityId,
            SecurityIdSource: securityIdSource,
            SecurityType: securityType,
            Currency: currency,
            QuoteCurrency: quoteCurrency,
            SourceMessageType: messageType);
    }

    private static bool IsCandidateMatch(
        LmaxReadOnlySecurityListDiscoveryInstrument instrument,
        string symbol,
        string slashSymbol)
    {
        var normalizedCandidate = NormalizeCompact(symbol);
        return NormalizeCompact(instrument.Symbol).Equals(normalizedCandidate, StringComparison.OrdinalIgnoreCase)
               || NormalizeCompact(instrument.SlashSymbol).Equals(normalizedCandidate, StringComparison.OrdinalIgnoreCase)
               || NormalizeCompact(instrument.Symbol).Equals(NormalizeCompact(slashSymbol), StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<(string Tag, string Value)> ParseFields(string message)
        => message.Split('\u0001', '|', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .Select(x => (x[0], x[1]))
            .ToArray();

    private static string? GetTag(string message, string tag)
        => ParseFields(message)
            .Where(x => x.Tag == tag)
            .Select(x => x.Value)
            .LastOrDefault();

    private static string? NormalizeSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return symbol.Replace("/", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private static string? NormalizeSlashSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var compact = NormalizeCompact(symbol);
        return compact.Length == 6 ? $"{compact[..3]}/{compact[3..]}" : symbol;
    }

    private static string NormalizeCompact(string? value)
        => (value ?? string.Empty).Replace("/", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

public static class LmaxReadOnlySecurityListDiscoveryRedactor
{
    private static readonly Regex SensitivePattern = new(
        "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.Compiled);

    public static string Redact(string? value, IEnumerable<string>? additionalSensitiveValues = null)
    {
        var redacted = LmaxReadOnlyFixMessageRedactor.Redact(value, additionalSensitiveValues);
        redacted = Regex.Replace(redacted, "(?i)(host\\s*[:=]\\s*)[^,;\\r\\n\\s]+", "$1[REDACTED]");
        redacted = Regex.Replace(redacted, "(?i)(account\\s*[:=]\\s*)[^,;\\r\\n\\s]+", "$1[REDACTED]");
        return redacted;
    }

    public static bool ContainsSensitiveContent(string? value)
        => !string.IsNullOrWhiteSpace(value) && SensitivePattern.IsMatch(value);
}

public static class LmaxReadOnlySecurityListDiscoveryArtifact
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToSanitizedJson(
        LmaxReadOnlySecurityListDiscoveryResult result,
        IEnumerable<string>? additionalSensitiveValues = null)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions);
        return LmaxReadOnlySecurityListDiscoveryRedactor.Redact(json, additionalSensitiveValues);
    }

    public static string Write(
        LmaxReadOnlySecurityListDiscoveryResult result,
        string artifactDirectory,
        IEnumerable<string>? additionalSensitiveValues = null)
    {
        Directory.CreateDirectory(artifactDirectory);
        var path = Path.Combine(artifactDirectory, $"{result.DiscoveryId}.json");
        File.WriteAllText(path, ToSanitizedJson(result, additionalSensitiveValues), Encoding.UTF8);
        return path;
    }
}

public static class LmaxReadOnlySecurityListDiscoveryArtifactValidator
{
    public static LmaxReadOnlySecurityListDiscoveryArtifactValidationResult ValidateJson(string json)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var status = GetString(root, "status") ?? "Unknown";
        var requestProfile = GetString(root, "requestProfile");
        var requestType = GetString(root, "securityListRequestType")
                          ?? LmaxReadOnlySecurityListDiscovery.ProfileDefinitions
                              .FirstOrDefault(x => x.ProfileName.Equals(requestProfile ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                              ?.SecurityListRequestType;
        var rejectMessageType = GetString(root, "rejectMessageType");
        var rejectTag = GetString(root, "rejectTag");
        var rejectText = LmaxReadOnlySecurityListDiscoveryRedactor.Redact(GetString(root, "rejectText")
                                                                          ?? GetArrayText(root, "errors"));
        var classification = Enum.TryParse<LmaxReadOnlySecurityListDiscoveryStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : LmaxReadOnlySecurityListDiscovery.ClassifyReject(rejectMessageType, rejectTag, rejectText, timedOut: false);

        var diagnostics = new LmaxReadOnlySecurityListFailureDiagnostics(
            Status: status,
            RejectMessageType: rejectMessageType,
            RejectTag: rejectTag,
            RejectText: rejectText,
            RequestProfile: requestProfile,
            SecurityListRequestType: requestType,
            LogonSucceeded: GetBool(root, "logonSucceeded"),
            SecurityListRequestAttempted: GetBool(root, "securityListRequestAttempted"),
            LogoutSucceeded: GetBool(root, "logoutSucceeded"),
            ResponseClassification: classification,
            NoSensitiveContent: GetBool(root, "noSensitiveContent"),
            CredentialValuesReturned: GetBool(root, "credentialValuesReturned"),
            OrderSubmissionAttempted: GetBool(root, "orderSubmissionAttempted"),
            ShadowReplaySubmitAttempted: GetBool(root, "shadowReplaySubmitAttempted"),
            TradingMutationAttempted: GetBool(root, "tradingMutationAttempted"),
            SchedulerStarted: GetBool(root, "schedulerStarted"),
            IsApprovedForExternalRun: GetBool(root, "isApprovedForExternalRun"));

        if (LmaxReadOnlySecurityListDiscoveryRedactor.ContainsSensitiveContent(json))
        {
            issues.Add(Error("SensitiveContentDetected", "$", "Discovery artifact contains credential-shaped or sensitive content."));
        }

        if (!diagnostics.NoSensitiveContent)
        {
            issues.Add(Error("SensitiveContentFlagFalse", "$.noSensitiveContent", "Discovery artifact must assert noSensitiveContent=true."));
        }

        if (diagnostics.CredentialValuesReturned)
        {
            issues.Add(Error("CredentialValuesReturned", "$.credentialValuesReturned", "Discovery artifact must not return credential values."));
        }

        if (diagnostics.OrderSubmissionAttempted)
        {
            issues.Add(Error("OrderSubmissionAttempted", "$.orderSubmissionAttempted", "SecurityList discovery must not submit orders."));
        }

        if (diagnostics.ShadowReplaySubmitAttempted)
        {
            issues.Add(Error("ShadowReplaySubmitAttempted", "$.shadowReplaySubmitAttempted", "SecurityList discovery must not submit to shadow replay."));
        }

        if (diagnostics.TradingMutationAttempted)
        {
            issues.Add(Error("TradingMutationAttempted", "$.tradingMutationAttempted", "SecurityList discovery must not mutate trading state."));
        }

        if (diagnostics.SchedulerStarted)
        {
            issues.Add(Error("SchedulerStarted", "$.schedulerStarted", "SecurityList discovery must not start a scheduler."));
        }

        if (diagnostics.IsApprovedForExternalRun)
        {
            issues.Add(Error("ExternalRunApprovalForbidden", "$.isApprovedForExternalRun", "Discovery artifacts must keep IsApprovedForExternalRun=false."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error)
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;
        return new(decision, diagnostics, issues);
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.True;

    private static string? GetArrayText(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(" ", value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()));
    }

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}

public static class LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator
{
    public static LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidationResult ValidateJson(string json)
    {
        var artifactValidation = LmaxReadOnlySecurityListDiscoveryArtifactValidator.ValidateJson(json);
        var issues = artifactValidation.Issues.ToList();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var attempts = ReadAttempts(root);
        var attemptedProfiles = attempts.Select(x => x.RequestProfile)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unmatchedCandidates = ReadStringArray(root, "unmatchedCandidates");
        var candidateMatchCount = GetCollectionCount(root, "candidateMatches");
        var finalStatus = GetString(root, "finalStatus")
                          ?? GetString(root, "status")
                          ?? "Unknown";
        var requestProfile = GetString(root, "requestProfile");
        var allProfilesFailedWithSameClass = attempts.Count > 1
                                             && attempts.Select(x => x.Classification).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                                             && attempts.All(x => !x.Classification.Equals(nameof(LmaxReadOnlySecurityListDiscoveryStatus.Completed), StringComparison.OrdinalIgnoreCase));
        var unsupportedClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedRequestType),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedSecurityRequestType),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedSymbolFilter),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedByVenue),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestTypeUnsupported),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListProfileRejected),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListNoSupportedProfiles),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListBusinessReject),
            nameof(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListSessionReject)
        };
        var likelyUnsupported = attempts.Count >= 2
                                && attempts.All(x => unsupportedClassifications.Contains(x.Classification))
                                && candidateMatchCount == 0;
        var missingRejectDiagnostics = finalStatus.Contains("UnknownReject", StringComparison.OrdinalIgnoreCase)
                                       && (attempts.Count == 0
                                           || attempts.All(x => string.IsNullOrWhiteSpace(x.RejectMessageType)
                                                               && string.IsNullOrWhiteSpace(x.RejectTag)
                                                               && string.IsNullOrWhiteSpace(x.RejectText)));

        var safeArtifact = artifactValidation.Decision != LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL;
        var decisionKind = ChooseDecision(
            safeArtifact,
            finalStatus,
            candidateMatchCount,
            unmatchedCandidates.Count,
            missingRejectDiagnostics,
            likelyUnsupported);
        var reason = BuildReason(decisionKind, finalStatus, attempts.Count, candidateMatchCount, unmatchedCandidates.Count, missingRejectDiagnostics, likelyUnsupported);
        var fallback = new LmaxReadOnlySecurityListDiscoveryFallbackDecision(
            RecommendedDecision: decisionKind,
            Reason: reason,
            FinalStatus: finalStatus,
            RequestProfile: requestProfile,
            Attempts: attempts,
            AttemptedProfiles: attemptedProfiles,
            AllProfilesFailedWithSameClass: allProfilesFailedWithSameClass,
            LikelySecurityListUnsupportedByVenue: likelyUnsupported,
            MissingRejectDiagnostics: missingRejectDiagnostics,
            CandidateMatchCount: candidateMatchCount,
            UnmatchedCandidates: unmatchedCandidates,
            IsApprovedForExternalRun: artifactValidation.Diagnostics.IsApprovedForExternalRun,
            ExternalRunAuthorized: false);

        if (fallback.ExternalRunAuthorized)
        {
            issues.Add(Error("FallbackExternalRunAuthorized", "$.fallbackDecision.externalRunAuthorized", "Fallback decisions must not authorize external run."));
        }

        if (fallback.IsApprovedForExternalRun)
        {
            issues.Add(Error("FallbackExternalRunApprovalForbidden", "$.fallbackDecision.isApprovedForExternalRun", "Fallback decisions must keep IsApprovedForExternalRun=false."));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error)
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;
        return new(decision, artifactValidation, fallback, issues);
    }

    private static LmaxReadOnlySecurityListFallbackDecisionKind ChooseDecision(
        bool safeArtifact,
        string finalStatus,
        int candidateMatchCount,
        int unmatchedCandidateCount,
        bool missingRejectDiagnostics,
        bool likelyUnsupported)
    {
        if (!safeArtifact)
        {
            return LmaxReadOnlySecurityListFallbackDecisionKind.BlockedPendingEvidence;
        }

        if (candidateMatchCount > 0 && unmatchedCandidateCount == 0)
        {
            return LmaxReadOnlySecurityListFallbackDecisionKind.BlockedPendingEvidence;
        }

        if (likelyUnsupported || missingRejectDiagnostics || finalStatus.Contains("UnknownReject", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlySecurityListFallbackDecisionKind.UseVendorSupportConfirmation;
        }

        return LmaxReadOnlySecurityListFallbackDecisionKind.BlockedPendingEvidence;
    }

    private static string BuildReason(
        LmaxReadOnlySecurityListFallbackDecisionKind decision,
        string finalStatus,
        int attemptCount,
        int candidateMatchCount,
        int unmatchedCandidateCount,
        bool missingRejectDiagnostics,
        bool likelyUnsupported)
    {
        if (decision == LmaxReadOnlySecurityListFallbackDecisionKind.UseVendorSupportConfirmation)
        {
            if (missingRejectDiagnostics)
            {
                return $"SecurityList discovery ended with {finalStatus}, produced no candidate matches, and the sanitized artifact has no reject tag/text or attempt diagnostics. Use vendor/support confirmation or other official manual evidence before creating confirmation records.";
            }

            if (likelyUnsupported)
            {
                return "Multiple safe SecurityList profiles failed without candidate matches; use vendor/support confirmation or other official manual evidence unless a future operator-approved diagnostic retry is explicitly chosen.";
            }

            return $"SecurityList discovery ended with {finalStatus} and did not identify the candidate SecurityIDs; use vendor/support confirmation or other official manual evidence.";
        }

        return $"SecurityList fallback remains blocked pending evidence. Attempts={attemptCount}; CandidateMatches={candidateMatchCount}; UnmatchedCandidates={unmatchedCandidateCount}.";
    }

    private static IReadOnlyList<LmaxReadOnlySecurityListDiscoveryAttemptDiagnostics> ReadAttempts(JsonElement root)
    {
        if (!root.TryGetProperty("attempts", out var attemptsElement) || attemptsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<LmaxReadOnlySecurityListDiscoveryAttemptDiagnostics>();
        }

        var attempts = new List<LmaxReadOnlySecurityListDiscoveryAttemptDiagnostics>();
        foreach (var attempt in attemptsElement.EnumerateArray())
        {
            var rejectText = LmaxReadOnlySecurityListDiscoveryRedactor.Redact(GetString(attempt, "rejectText"));
            var status = GetString(attempt, "status")
                         ?? GetString(attempt, "classification")
                         ?? LmaxReadOnlySecurityListDiscovery.ClassifyReject(
                             GetString(attempt, "rejectMessageType") ?? GetString(attempt, "firstInboundMessageType"),
                             GetString(attempt, "rejectTag"),
                             rejectText,
                             timedOut: false).ToString();
            attempts.Add(new(
                RequestProfile: GetString(attempt, "requestProfile") ?? "Unknown",
                FirstInboundMessageType: GetString(attempt, "firstInboundMessageType"),
                RejectMessageType: GetString(attempt, "rejectMessageType"),
                RejectTag: GetString(attempt, "rejectTag"),
                RejectText: rejectText,
                Classification: status,
                InstrumentCount: GetInt(attempt, "instrumentCount"),
                CandidateMatchCount: GetCollectionCount(attempt, "candidateMatches")));
        }

        return attempts;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static int GetCollectionCount(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.GetArrayLength(),
            JsonValueKind.Object => value.EnumerateObject().Any() ? 1 : 0,
            _ => 0
        };
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}
