namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlyExternalSession
{
    Task<LmaxReadOnlyExternalSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyExternalSessionSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyExternalSessionResult> RunAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyExternalSessionFactory
{
    ILmaxReadOnlyExternalSession CreateDisabledSession();
}

public interface ILmaxReadOnlyExternalSessionSafetyGate
{
    LmaxReadOnlyExternalSessionSafetyEvaluation Evaluate(LmaxReadOnlyRuntimeAdapterOptions options, LmaxReadOnlyExternalSessionRequest request);
}

public enum LmaxReadOnlyExternalSessionEventType
{
    MarketDataSnapshot,
    TradeCaptureReport,
    OrderStatusReport,
    ProtocolReject,
    SessionWarning,
    SessionError
}

public sealed record LmaxReadOnlyExternalSessionRequest(
    string? Reason,
    int? MaxEvents = null,
    int? MaxRuntimeSeconds = null,
    LmaxReadOnlyRuntimeActivationLevel RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
    bool PreviewEvidence = false);

public sealed record LmaxReadOnlyExternalSessionCounters(
    int MarketDataSnapshotCount,
    int TradeCaptureReportCount,
    int OrderStatusReportCount,
    int ProtocolRejectCount,
    int SessionWarningCount,
    int SessionErrorCount)
{
    public int TotalEventCount =>
        MarketDataSnapshotCount
        + TradeCaptureReportCount
        + OrderStatusReportCount
        + ProtocolRejectCount
        + SessionWarningCount
        + SessionErrorCount;
}

public sealed record LmaxReadOnlyExternalSessionEvent(
    string EventId,
    LmaxReadOnlyExternalSessionEventType EventType,
    DateTimeOffset ObservedAtUtc,
    string SanitizedPayloadJson,
    string? ClientOrderId = null,
    string? BrokerOrderId = null,
    string? BrokerExecutionId = null,
    string? InstrumentId = null,
    string? Symbol = null);

public sealed record LmaxReadOnlyExternalSessionReject(
    string RejectId,
    DateTimeOffset ObservedAtUtc,
    string RejectContext,
    string Message,
    string? RefMsgType = null,
    string? RefSeqNum = null,
    string? SanitizedPayloadJson = null);

public sealed record LmaxReadOnlyExternalSessionStatus(
    LmaxReadOnlyRuntimeRunStatus Status,
    LmaxReadOnlyRuntimeImplementationMode ImplementationMode,
    LmaxReadOnlyRuntimeActivationLevel ActivationLevel,
    bool ExternalSessionImplementationAvailable,
    bool SocketImplementationAvailable,
    bool ReadOnly,
    string Message,
    LmaxReadOnlyExternalSessionCounters Counters,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates);

public sealed record LmaxReadOnlyExternalSessionResult(
    LmaxReadOnlyRuntimeRunStatus Status,
    string Message,
    bool ExternalSessionImplementationAvailable,
    bool SocketOpened,
    bool CredentialsUsed,
    bool EvidenceCreated,
    bool SubmittedToShadowReplay,
    LmaxReadOnlyExternalSessionCounters Counters,
    LmaxReadOnlyExternalSessionSafetyEvaluation Safety)
{
    public LmaxReadOnlyExternalSessionEvidencePreviewResult? EvidencePreview { get; init; }
}

public sealed record LmaxReadOnlyExternalSessionSafetyEvaluation(
    LmaxReadOnlyRuntimeRunStatus RunStatus,
    string BlockedReason,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates)
{
    public bool Passed => !Gates.Any(x => x.BlocksRun);
    public IReadOnlyList<string> FailedGateNames => Gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
}

public sealed class LmaxReadOnlyExternalSessionSafetyGate(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyExternalSessionSafetyGate
{
    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();

    public LmaxReadOnlyExternalSessionSafetyEvaluation Evaluate(LmaxReadOnlyRuntimeAdapterOptions options, LmaxReadOnlyExternalSessionRequest request)
        => EvaluateCore(options, request);

    public LmaxReadOnlyExternalSessionSafetyEvaluation Evaluate(LmaxReadOnlyExternalSessionRequest request)
        => EvaluateCore(_options, request);

    private static LmaxReadOnlyExternalSessionSafetyEvaluation EvaluateCore(
        LmaxReadOnlyRuntimeAdapterOptions options,
        LmaxReadOnlyExternalSessionRequest request)
    {
        var runtimeRequest = new LmaxReadOnlyRuntimeRunRequest(
            request.Reason,
            request.MaxEvents,
            request.MaxRuntimeSeconds,
            DryRun: true,
            request.RequestedActivationLevel);
        var runtimeEvaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options, runtimeRequest);
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>(runtimeEvaluation.Gates)
        {
            Gate("ExternalSessionActivationLevel", request.RequestedActivationLevel == LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit, "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit", request.RequestedActivationLevel.ToString()),
            Gate("ExternalSessionImplementationAvailable", false, "true only after a separate Phase 4 implementation gate", "false - Phase 4A defines contracts and a disabled stub only."),
            Gate("ExternalSessionSocketImplementation", false, "not present in Phase 4A", "false - no socket, FIX logon, or network client exists."),
            Gate("ExternalSessionCredentialUse", false, "not used in Phase 4A", "false - no credentials are read or passed.")
        };

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "All current external-session gates passed, but Phase 4A still has no implementation to run."
            : "Blocked by external-session safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyExternalSessionSafetyEvaluation(status, reason, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}

public sealed class LmaxReadOnlyExternalSessionDisabled(
    LmaxReadOnlyRuntimeAdapterOptions? options = null,
    ILmaxReadOnlyExternalSessionSafetyGate? safetyGate = null) : ILmaxReadOnlyExternalSession
{
    private static readonly LmaxReadOnlyExternalSessionCounters EmptyCounters = new(0, 0, 0, 0, 0, 0);

    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();
    private readonly ILmaxReadOnlyExternalSessionSafetyGate _safetyGate = safetyGate ?? new LmaxReadOnlyExternalSessionSafetyGate();

    public Task<LmaxReadOnlyExternalSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = _safetyGate.Evaluate(_options, new LmaxReadOnlyExternalSessionRequest("status check"));
        return Task.FromResult(new LmaxReadOnlyExternalSessionStatus(
            safety.RunStatus,
            _options.ImplementationMode,
            _options.RequestedActivationLevel,
            ExternalSessionImplementationAvailable: false,
            SocketImplementationAvailable: false,
            ReadOnly: true,
            "LMAX read-only external session Phase 4A is contract/stub only. No socket, FIX logon, credentials, evidence creation, shadow replay submit, or trading-state mutation exists.",
            EmptyCounters,
            safety.Gates));
    }

    public Task<LmaxReadOnlyExternalSessionSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(_safetyGate.Evaluate(_options, request));

    public Task<LmaxReadOnlyExternalSessionResult> RunAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default)
    {
        var safety = _safetyGate.Evaluate(_options, request);
        var status = safety.RunStatus == LmaxReadOnlyRuntimeRunStatus.Disabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : LmaxReadOnlyRuntimeRunStatus.Blocked;
        return Task.FromResult(new LmaxReadOnlyExternalSessionResult(
            status,
            "LMAX read-only external session is disabled/not started for Phase 4A: ExternalSessionImplementationNotStarted. No sockets were opened, no credentials were used, no evidence was created, no shadow replay submit occurred, and no trading state was mutated. " + safety.BlockedReason,
            ExternalSessionImplementationAvailable: false,
            SocketOpened: false,
            CredentialsUsed: false,
            EvidenceCreated: false,
            SubmittedToShadowReplay: false,
            EmptyCounters,
            safety));
    }
}

public sealed class LmaxReadOnlyExternalSessionDisabledFactory(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyExternalSessionFactory
{
    public ILmaxReadOnlyExternalSession CreateDisabledSession()
        => new LmaxReadOnlyExternalSessionDisabled(options);
}
