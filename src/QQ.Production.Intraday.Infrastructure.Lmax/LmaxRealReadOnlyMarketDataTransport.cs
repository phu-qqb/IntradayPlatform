namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyBoundaryStepResult(
    LmaxTemporaryReadOnlySessionBoundaryStatus Status,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage)
{
    public bool Succeeded =>
        Status is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
            or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded;
}

public sealed record LmaxReadOnlyMarketDataSessionClientResult(
    IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> InstrumentStatuses,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage)
{
    public bool MarketDataRequestWriteAttempted { get; init; }

    public bool MarketDataRequestWriteSucceeded { get; init; }

    public bool MarketDataRequestResponseReadAttempted { get; init; }

    public bool MarketDataRequestReachedBoundedResponseClassification { get; init; }

    public string? MarketDataRejectSanitizedSubcategory { get; init; }

    public string? SessionRejectSanitizedSubcategory { get; init; }

    public string? RejectReasonExtractionSource { get; init; }

    public string? SessionRejectRefTagIdSanitizedCategory { get; init; }

    public string? SessionRejectReasonSanitizedCategory { get; init; }

    public string? SessionRejectRefMsgTypeSanitizedCategory { get; init; }

    public bool? MarketDataEntriesObserved { get; init; }

    public int? MarketDataSanitizedEntryCount { get; init; }

    public string? MarketDataEntriesEvidenceCategory { get; init; }

    public string? MarketDataEntriesReportingSource { get; init; }

    public string? MarketDataEntriesNotAvailableReason { get; init; }

    public bool LogoutObserved { get; init; }

    public string? LogoutSourceCategory { get; init; }

    public string? LogoutReasonSanitizedCategory { get; init; }

    public bool? LogoutTextPresentSanitized { get; init; }

    public string? LogoutAfterInstrument { get; init; }

    public string? LogoutAfterSecurityIdSanitized { get; init; }

    public string? LogoutTimingCategory { get; init; }

    public string? LogoutReasonExtractionSource { get; init; }
}

public interface ILmaxReadOnlyMarketDataSessionClient
{
    LmaxReadOnlyBoundaryStepResult OpenReadOnlyTcpBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    LmaxReadOnlyBoundaryStepResult OpenReadOnlyTlsBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    LmaxReadOnlyBoundaryStepResult OpenReadOnlyFixLogonBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public sealed class LmaxRealReadOnlyMarketDataTransport : ILmaxTemporaryReadOnlyMarketDataTransport
{
    private readonly ILmaxReadOnlyMarketDataSessionClient sessionClient;
    private bool sessionStarted;
    private bool shutdownRevertCompleted = true;

    public LmaxRealReadOnlyMarketDataTransport(ILmaxReadOnlyMarketDataSessionClient sessionClient)
    {
        this.sessionClient = sessionClient ?? throw new ArgumentNullException(nameof(sessionClient));
    }

    public LmaxTemporaryReadOnlyTransportResult RunAsync(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var safetyIssues = ValidateBeforeClientUse(scope);
        if (safetyIssues.Count > 0)
        {
            return Blocked(startedAtUtc, safetyIssues);
        }

        LmaxReadOnlyBoundaryStepResult tcp;
        LmaxReadOnlyBoundaryStepResult tls = NotAttempted("TlsNotAttempted", "TLS boundary was not reached.");
        LmaxReadOnlyBoundaryStepResult fix = NotAttempted("FixLogonNotAttempted", "FIX logon boundary was not reached.");
        LmaxReadOnlyBoundaryStepResult marketData = NotAttempted("MarketDataNotAttempted", "Market-data boundary was not reached.");
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> instrumentStatuses = [];
        string sanitizedStatus;
        string? errorCategory;
        string? errorMessage;

        tcp = sessionClient.OpenReadOnlyTcpBoundary(scope, cancellationToken);
        sessionStarted = true;
        if (!tcp.Succeeded)
        {
            ShutdownIfStarted();
            sanitizedStatus = "ReadOnlyTcpBoundaryFailedSanitized";
            errorCategory = tcp.SanitizedErrorCategory ?? "TcpBoundaryFailed";
            errorMessage = tcp.SanitizedErrorMessage;
            return Result(startedAtUtc, tcp, tls, fix, marketData, instrumentStatuses, sanitizedStatus, errorCategory, errorMessage);
        }

        tls = sessionClient.OpenReadOnlyTlsBoundary(scope, cancellationToken);
        if (!tls.Succeeded)
        {
            ShutdownIfStarted();
            sanitizedStatus = "ReadOnlyTlsBoundaryFailedSanitized";
            errorCategory = tls.SanitizedErrorCategory ?? "TlsBoundaryFailed";
            errorMessage = tls.SanitizedErrorMessage;
            return Result(startedAtUtc, tcp, tls, fix, marketData, instrumentStatuses, sanitizedStatus, errorCategory, errorMessage);
        }

        fix = sessionClient.OpenReadOnlyFixLogonBoundary(scope, cancellationToken);
        if (!fix.Succeeded)
        {
            ShutdownIfStarted();
            sanitizedStatus = "ReadOnlyFixLogonBoundaryFailedSanitized";
            errorCategory = fix.SanitizedErrorCategory ?? "FixLogonBoundaryFailed";
            errorMessage = fix.SanitizedErrorMessage;
            return Result(startedAtUtc, tcp, tls, fix, marketData, instrumentStatuses, sanitizedStatus, errorCategory, errorMessage);
        }

        var marketDataResult = sessionClient.RequestReadOnlyMarketData(scope, cancellationToken);
        instrumentStatuses = SanitizeInstrumentStatuses(scope, marketDataResult.InstrumentStatuses);
        marketData = instrumentStatuses.All(x => x.MarketDataBoundary is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)
            ? new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, "ReadOnlyMarketDataBoundarySucceededSanitized", null, null)
            : new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, "ReadOnlyMarketDataBoundaryFailedSanitized", marketDataResult.SanitizedErrorCategory ?? "MarketDataBoundaryFailed", marketDataResult.SanitizedErrorMessage);

        ShutdownIfStarted();
        sanitizedStatus = marketData.Succeeded ? "ReadOnlyTransportCompletedWithSanitizedEvidence" : "ReadOnlyTransportMarketDataFailedSanitized";
        errorCategory = marketData.Succeeded ? null : marketData.SanitizedErrorCategory;
        errorMessage = marketData.Succeeded ? null : marketData.SanitizedErrorMessage;
        return Result(startedAtUtc, tcp, tls, fix, marketData, instrumentStatuses, sanitizedStatus, errorCategory, errorMessage) with
        {
            MarketDataRequestWriteAttempted = marketDataResult.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = marketDataResult.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = marketDataResult.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = marketDataResult.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRequestSentLegacyFlag = IsRealBoundaryAttempt(marketData.Status),
            MarketDataRejectSanitizedSubcategory = marketDataResult.MarketDataRejectSanitizedSubcategory,
            SessionRejectSanitizedSubcategory = marketDataResult.SessionRejectSanitizedSubcategory,
            RejectReasonExtractionSource = marketDataResult.RejectReasonExtractionSource,
            SessionRejectRefTagIdSanitizedCategory = marketDataResult.SessionRejectRefTagIdSanitizedCategory,
            SessionRejectReasonSanitizedCategory = marketDataResult.SessionRejectReasonSanitizedCategory,
            SessionRejectRefMsgTypeSanitizedCategory = marketDataResult.SessionRejectRefMsgTypeSanitizedCategory,
            MarketDataEntriesObserved = marketDataResult.MarketDataEntriesObserved ?? EntriesObservedFromStatuses(instrumentStatuses),
            MarketDataSanitizedEntryCount = marketDataResult.MarketDataSanitizedEntryCount ?? EntryCountFromStatuses(instrumentStatuses),
            MarketDataEntriesEvidenceCategory = marketDataResult.MarketDataEntriesEvidenceCategory ?? EntriesEvidenceCategoryFromStatuses(instrumentStatuses),
            MarketDataEntriesReportingSource = marketDataResult.MarketDataEntriesReportingSource ?? "TransportInstrumentStatuses",
            MarketDataEntriesNotAvailableReason = marketDataResult.MarketDataEntriesNotAvailableReason,
            LogoutObserved = marketDataResult.LogoutObserved,
            LogoutSourceCategory = marketDataResult.LogoutSourceCategory,
            LogoutReasonSanitizedCategory = marketDataResult.LogoutReasonSanitizedCategory,
            LogoutTextPresentSanitized = marketDataResult.LogoutTextPresentSanitized,
            LogoutAfterInstrument = marketDataResult.LogoutAfterInstrument,
            LogoutAfterSecurityIdSanitized = marketDataResult.LogoutAfterSecurityIdSanitized,
            LogoutTimingCategory = marketDataResult.LogoutTimingCategory,
            LogoutReasonExtractionSource = marketDataResult.LogoutReasonExtractionSource
        };
    }

    public void ShutdownRevert()
    {
        ShutdownIfStarted();
    }

    private void ShutdownIfStarted()
    {
        if (sessionStarted)
        {
            shutdownRevertCompleted = sessionClient.ShutdownRevert();
            sessionStarted = false;
        }
    }

    private static List<LmaxReadOnlyRuntimePreflightIssue> ValidateBeforeClientUse(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);
        issues.AddRange(gate.Issues);

        foreach (var instrument in scope.Instruments)
        {
            if (LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol) is null)
            {
                Add(issues, "NonApprovedInstrument", "$.instruments", $"Instrument '{instrument.Symbol}' is not approved for read-only runtime activation.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "UsdJpyCaveatMissing", "$.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
            }
        }

        return issues;
    }

    private LmaxTemporaryReadOnlyTransportResult Blocked(
        DateTimeOffset startedAtUtc,
        IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> safetyIssues)
        => new(
            startedAtUtc,
            DateTimeOffset.UtcNow,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            [],
            OutputSanitized: true,
            CredentialsLoaded: false,
            CredentialsPrinted: false,
            CredentialsStored: false,
            ShutdownRevertCompleted: true,
            "ReadOnlyTransportBlockedBeforeSessionClientUse",
            "SafetyConstraintFailed",
            string.Join("; ", safetyIssues.Select(x => x.Code)));

    private LmaxTemporaryReadOnlyTransportResult Result(
        DateTimeOffset startedAtUtc,
        LmaxReadOnlyBoundaryStepResult tcp,
        LmaxReadOnlyBoundaryStepResult tls,
        LmaxReadOnlyBoundaryStepResult fix,
        LmaxReadOnlyBoundaryStepResult marketData,
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> instrumentStatuses,
        string sanitizedStatus,
        string? sanitizedErrorCategory,
        string? sanitizedErrorMessage)
        => new LmaxTemporaryReadOnlyTransportResult(
            startedAtUtc,
            DateTimeOffset.UtcNow,
            tcp.Status,
            tls.Status,
            fix.Status,
            marketData.Status,
            instrumentStatuses,
            OutputSanitized: true,
            CredentialsLoaded: false,
            CredentialsPrinted: false,
            CredentialsStored: false,
            ShutdownRevertCompleted: shutdownRevertCompleted,
            sanitizedStatus,
            sanitizedErrorCategory,
            sanitizedErrorMessage)
        {
            TcpBoundarySanitizedStatus = tcp.SanitizedStatus,
            TcpBoundarySanitizedErrorCategory = tcp.SanitizedErrorCategory,
            TlsBoundarySanitizedStatus = tls.SanitizedStatus,
            TlsBoundarySanitizedErrorCategory = tls.SanitizedErrorCategory,
            FixBoundarySanitizedStatus = fix.SanitizedStatus,
            FixBoundarySanitizedErrorCategory = fix.SanitizedErrorCategory,
            MarketDataBoundarySanitizedStatus = marketData.SanitizedStatus,
            MarketDataBoundarySanitizedErrorCategory = marketData.SanitizedErrorCategory
        };

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
                    "ReadOnlyInstrumentStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return a sanitized market-data status.",
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
                status.SanitizedStatus,
                status.SanitizedErrorCategory,
                status.SanitizedErrorMessage,
                instrument.Caveat);
        }).ToList();
    }

    private static LmaxReadOnlyBoundaryStepResult NotAttempted(string category, string message)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, "NotAttempted", category, message);

    private static bool IsRealBoundaryAttempt(LmaxTemporaryReadOnlySessionBoundaryStatus status)
        => status is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
            or LmaxTemporaryReadOnlySessionBoundaryStatus.Failed;

    private static bool? EntriesObservedFromStatuses(IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
        => statuses.Count == 0 ? null : statuses.Sum(x => Math.Max(0, x.MarketDataSnapshotCount)) > 0;

    private static int? EntryCountFromStatuses(IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
        => statuses.Count == 0 ? null : statuses.Sum(x => Math.Max(0, x.MarketDataSnapshotCount));

    private static string EntriesEvidenceCategoryFromStatuses(IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return "EntriesEvidenceInconclusiveSafe";
        }

        return statuses.Sum(x => Math.Max(0, x.MarketDataSnapshotCount)) > 0
            ? "EntriesObservedWithSanitizedCount"
            : "NoEntriesObserved";
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
