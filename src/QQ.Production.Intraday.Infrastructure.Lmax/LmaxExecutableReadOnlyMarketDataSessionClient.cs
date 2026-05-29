namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlySocketSessionBoundary
{
    LmaxReadOnlyBoundaryStepResult OpenTcpBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    LmaxReadOnlyBoundaryStepResult OpenTlsBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public interface ILmaxReadOnlyFixSessionBoundary
{
    LmaxReadOnlyBoundaryStepResult OpenFixLogonBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyMarketDataRequestCodec
{
    LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxExecutableReadOnlyMarketDataSessionClient : ILmaxReadOnlyMarketDataSessionClient
{
    private readonly ILmaxReadOnlySocketSessionBoundary socketSession;
    private readonly ILmaxReadOnlyFixSessionBoundary fixSession;
    private readonly ILmaxReadOnlyMarketDataRequestCodec marketDataCodec;
    private bool tcpOpened;
    private bool tlsOpened;
    private bool fixLogonOpened;

    public LmaxExecutableReadOnlyMarketDataSessionClient(
        ILmaxReadOnlySocketSessionBoundary socketSession,
        ILmaxReadOnlyFixSessionBoundary fixSession,
        ILmaxReadOnlyMarketDataRequestCodec marketDataCodec)
    {
        this.socketSession = socketSession ?? throw new ArgumentNullException(nameof(socketSession));
        this.fixSession = fixSession ?? throw new ArgumentNullException(nameof(fixSession));
        this.marketDataCodec = marketDataCodec ?? throw new ArgumentNullException(nameof(marketDataCodec));
    }

    public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTcpBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeLowLevelUse(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        var result = socketSession.OpenTcpBoundary(scope, cancellationToken);
        tcpOpened = result.Succeeded;
        return SanitizeBoundaryResult(result, "TcpBoundary");
    }

    public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTlsBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeLowLevelUse(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        if (!tcpOpened)
        {
            return NotAttempted("TcpBoundaryNotOpened", "TCP boundary must succeed before TLS boundary.");
        }

        var result = socketSession.OpenTlsBoundary(scope, cancellationToken);
        tlsOpened = result.Succeeded;
        return SanitizeBoundaryResult(result, "TlsBoundary");
    }

    public LmaxReadOnlyBoundaryStepResult OpenReadOnlyFixLogonBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeLowLevelUse(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        if (!tlsOpened)
        {
            return NotAttempted("TlsBoundaryNotOpened", "TLS boundary must succeed before FIX logon boundary.");
        }

        var result = fixSession.OpenFixLogonBoundary(scope, cancellationToken);
        fixLogonOpened = result.Succeeded;
        return SanitizeBoundaryResult(result, "FixLogonBoundary");
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeLowLevelUse(scope);
        if (blocked is not null)
        {
            return new LmaxReadOnlyMarketDataSessionClientResult(
                [],
                "ReadOnlyMarketDataBlockedBeforeLowLevelUse",
                blocked.SanitizedErrorCategory,
                blocked.SanitizedErrorMessage);
        }

        if (!fixLogonOpened)
        {
            return new LmaxReadOnlyMarketDataSessionClientResult(
                [],
                "ReadOnlyMarketDataNotAttempted",
                "FixLogonBoundaryNotOpened",
                "FIX logon boundary must succeed before read-only market-data request.");
        }

        var result = marketDataCodec.RequestReadOnlyMarketData(scope, cancellationToken);
        return new LmaxReadOnlyMarketDataSessionClientResult(
            SanitizeInstrumentStatuses(scope, result.InstrumentStatuses),
            SanitizeText(result.SanitizedStatus, "ReadOnlyMarketDataStatusSanitized")!,
            SanitizeText(result.SanitizedErrorCategory, null),
            SanitizeText(result.SanitizedErrorMessage, null))
        {
            MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRejectSanitizedSubcategory = SanitizeText(result.MarketDataRejectSanitizedSubcategory, null),
            SessionRejectSanitizedSubcategory = SanitizeText(result.SessionRejectSanitizedSubcategory, null),
            RejectReasonExtractionSource = SanitizeText(result.RejectReasonExtractionSource, null),
            SessionRejectRefTagIdSanitizedCategory = SanitizeText(result.SessionRejectRefTagIdSanitizedCategory, null),
            SessionRejectReasonSanitizedCategory = SanitizeText(result.SessionRejectReasonSanitizedCategory, null),
            SessionRejectRefMsgTypeSanitizedCategory = SanitizeText(result.SessionRejectRefMsgTypeSanitizedCategory, null),
            MarketDataEntriesObserved = result.MarketDataEntriesObserved,
            MarketDataSanitizedEntryCount = result.MarketDataSanitizedEntryCount,
            MarketDataEntriesEvidenceCategory = SanitizeText(result.MarketDataEntriesEvidenceCategory, null),
            MarketDataEntriesReportingSource = SanitizeText(result.MarketDataEntriesReportingSource, null),
            MarketDataEntriesNotAvailableReason = SanitizeText(result.MarketDataEntriesNotAvailableReason, null),
            LogoutObserved = result.LogoutObserved,
            LogoutSourceCategory = SanitizeText(result.LogoutSourceCategory, null),
            LogoutReasonSanitizedCategory = SanitizeText(result.LogoutReasonSanitizedCategory, null),
            LogoutTextPresentSanitized = result.LogoutTextPresentSanitized,
            LogoutAfterInstrument = SanitizeText(result.LogoutAfterInstrument, null),
            LogoutAfterSecurityIdSanitized = SanitizeText(result.LogoutAfterSecurityIdSanitized, null),
            LogoutTimingCategory = SanitizeText(result.LogoutTimingCategory, null),
            LogoutReasonExtractionSource = SanitizeText(result.LogoutReasonExtractionSource, null)
        };
    }

    public bool ShutdownRevert()
    {
        var completed = socketSession.ShutdownRevert();
        tcpOpened = false;
        tlsOpened = false;
        fixLogonOpened = false;
        return completed;
    }

    private static LmaxReadOnlyBoundaryStepResult? ValidateBeforeLowLevelUse(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>(gate.Issues);

        foreach (var instrument in scope.Instruments)
        {
            if (LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol) is null)
            {
                Add(issues, "NonApprovedInstrument", "$.instruments", $"Instrument '{instrument.Symbol}' is not approved for read-only session use.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "UsdJpyCaveatMissing", "$.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
            }
        }

        if (issues.Count == 0)
        {
            return null;
        }

        return new LmaxReadOnlyBoundaryStepResult(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            "ReadOnlySessionClientBlockedBeforeLowLevelUse",
            "SafetyConstraintFailed",
            string.Join("; ", issues.Select(x => x.Code)));
    }

    private static IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> SanitizeInstrumentStatuses(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
    {
        var statusBySymbol = statuses.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        return scope.Instruments.Select(instrument =>
        {
            if (!statusBySymbol.TryGetValue(instrument.Symbol, out var status))
            {
                return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 0,
                    "ExecutableSessionClientInstrumentStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return a sanitized read-only market-data status.",
                    instrument.Caveat);
            }

            return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                status.MarketDataBoundary,
                status.MarketDataSnapshotCount,
                status.MarketDataRequestRejectCount,
                status.BusinessMessageRejectCount,
                status.SessionRejectCount,
                SanitizeText(status.SanitizedStatus, "ExecutableSessionClientInstrumentStatusSanitized")!,
                SanitizeText(status.SanitizedErrorCategory, null),
                SanitizeText(status.SanitizedErrorMessage, null),
                instrument.Caveat);
        }).ToList();
    }

    private static LmaxReadOnlyBoundaryStepResult SanitizeBoundaryResult(
        LmaxReadOnlyBoundaryStepResult result,
        string defaultCategory)
        => new(
            result.Status,
            SanitizeText(result.SanitizedStatus, "ReadOnlyBoundaryStatusSanitized")!,
            SanitizeText(result.SanitizedErrorCategory, defaultCategory),
            SanitizeText(result.SanitizedErrorMessage, null));

    private static LmaxReadOnlyBoundaryStepResult NotAttempted(string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            "NotAttempted",
            category,
            message);

    private static string? SanitizeText(string? value, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var sanitized = value
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "554=[redacted]", StringComparison.OrdinalIgnoreCase);

        return sanitized;
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
