namespace QQ.Production.Intraday.Infrastructure.Lmax;

public static class LmaxReadOnlyRuntimeAdapterOptions
{
    public const int SafeMaxRuntimeSeconds = 300;
}

public enum LmaxTemporaryReadOnlySessionBoundaryStatus
{
    NotAttempted,
    FakeSucceeded,
    FakeFailed,
    Succeeded,
    Failed
}

public sealed record LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
    string Symbol,
    string? SecurityId,
    string? SecurityIdSource,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataBoundary,
    int MarketDataSnapshotCount,
    int MarketDataRequestRejectCount,
    int BusinessMessageRejectCount,
    int SessionRejectCount,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage,
    string? Caveat);

public sealed record LmaxTemporaryReadOnlyTransportResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataBoundary,
    IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> InstrumentStatuses,
    bool OutputSanitized,
    bool CredentialsLoaded,
    bool CredentialsPrinted,
    bool CredentialsStored,
    bool ShutdownRevertCompleted,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage)
{
    public string? TcpBoundarySanitizedStatus { get; init; }

    public string? TcpBoundarySanitizedErrorCategory { get; init; }

    public string? TlsBoundarySanitizedStatus { get; init; }

    public string? TlsBoundarySanitizedErrorCategory { get; init; }

    public string? FixBoundarySanitizedStatus { get; init; }

    public string? FixBoundarySanitizedErrorCategory { get; init; }

    public string? MarketDataBoundarySanitizedStatus { get; init; }

    public string? MarketDataBoundarySanitizedErrorCategory { get; init; }

    public bool MarketDataRequestWriteAttempted { get; init; }

    public bool MarketDataRequestWriteSucceeded { get; init; }

    public bool MarketDataRequestResponseReadAttempted { get; init; }

    public bool MarketDataRequestReachedBoundedResponseClassification { get; init; }

    public bool MarketDataRequestSentLegacyFlag { get; init; }

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

public interface ILmaxTemporaryReadOnlyMarketDataTransport
{
    LmaxTemporaryReadOnlyTransportResult RunAsync(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    void ShutdownRevert();
}
