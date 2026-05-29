namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlyRuntimeAdapter
{
    Task<LmaxReadOnlyRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyRuntimeSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyRuntimeRunRequest? request = null, CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyRuntimeRunResult> RunAsync(LmaxReadOnlyRuntimeRunRequest request, CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyRuntimeSafetyGate
{
    LmaxReadOnlyRuntimeSafetyEvaluation Evaluate(LmaxReadOnlyRuntimeAdapterOptions options, LmaxReadOnlyRuntimeRunRequest? request = null);
}

public interface ILmaxReadOnlyRuntimeEvidenceSink
{
    Task<LmaxReadOnlyRuntimeEvidenceSinkResult> AcceptEvidenceBatchAsync(LmaxReadOnlyRuntimeEvidenceBatchPreview batch, CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyRuntimeRunStore
{
    Task RecordRunAttemptAsync(LmaxReadOnlyRuntimeRunResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LmaxReadOnlyRuntimeRunResult>> GetRecentRunsAsync(int limit = 20, CancellationToken cancellationToken = default);
}

public enum LmaxReadOnlyRuntimeRunMode
{
    StatusOnly,
    SafetyEvaluationOnly,
    DisabledDryRun,
    FakeInMemoryFixtureOnly,
    FakeTransportPreview,
    FutureFakeEvidence,
    FutureReadOnlyCapture
}

public sealed record LmaxReadOnlyRuntimeStatus(
    LmaxReadOnlyRuntimeImplementationMode ImplementationMode,
    LmaxReadOnlyRuntimeActivationLevel ActivationLevel,
    LmaxReadOnlyRuntimeRunStatus Status,
    bool Enabled,
    bool ReadOnly,
    bool AllowExternalConnections,
    bool AllowCredentialUse,
    bool AllowOrderSubmission,
    bool PersistRawFixMessages,
    bool PersistToTradingTables,
    bool SubmitToShadowReplay,
    bool SchedulerEnabled,
    string Message,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates);

public sealed record LmaxReadOnlyRuntimeEvidenceBatchPreview(
    string BatchId,
    string SchemaVersion,
    string EvidenceMode,
    DateTimeOffset CreatedAtUtc,
    int ExecutionReportCount,
    int OrderStatusCount,
    int TradeCaptureReportCount,
    int ProtocolRejectCount,
    int MarketDataSnapshotCount,
    bool Sanitized,
    bool ContainsRawFix,
    IReadOnlyList<string> Warnings);

public sealed record LmaxReadOnlyRuntimeEvidenceSinkResult(
    bool Accepted,
    bool SubmittedToShadowReplay,
    string Message);

public sealed class LmaxReadOnlyRuntimeSafetyGateEvaluator : ILmaxReadOnlyRuntimeSafetyGate
{
    public LmaxReadOnlyRuntimeSafetyEvaluation Evaluate(LmaxReadOnlyRuntimeAdapterOptions options, LmaxReadOnlyRuntimeRunRequest? request = null)
        => LmaxReadOnlyRuntimeSafetyGate.Evaluate(options, request);
}

public sealed class LmaxReadOnlyRuntimeEvidenceSinkDisabled : ILmaxReadOnlyRuntimeEvidenceSink
{
    public Task<LmaxReadOnlyRuntimeEvidenceSinkResult> AcceptEvidenceBatchAsync(LmaxReadOnlyRuntimeEvidenceBatchPreview batch, CancellationToken cancellationToken = default)
        => Task.FromResult(new LmaxReadOnlyRuntimeEvidenceSinkResult(
            Accepted: false,
            SubmittedToShadowReplay: false,
            "LMAX read-only runtime evidence sink is disabled for Phase 1 and does not submit evidence."));
}

public sealed class LmaxReadOnlyRuntimeRunStoreNoOp : ILmaxReadOnlyRuntimeRunStore
{
    public Task RecordRunAttemptAsync(LmaxReadOnlyRuntimeRunResult result, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<LmaxReadOnlyRuntimeRunResult>> GetRecentRunsAsync(int limit = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LmaxReadOnlyRuntimeRunResult>>([]);
}

public sealed class LmaxReadOnlyRuntimeAdapterDisabled(
    LmaxReadOnlyRuntimeAdapterOptions? options = null,
    ILmaxReadOnlyRuntimeSafetyGate? safetyGate = null,
    ILmaxReadOnlyRuntimeEvidenceSink? evidenceSink = null,
    ILmaxReadOnlyRuntimeRunStore? runStore = null) : ILmaxReadOnlyRuntimeAdapter
{
    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();
    private readonly ILmaxReadOnlyRuntimeSafetyGate _safetyGate = safetyGate ?? new LmaxReadOnlyRuntimeSafetyGateEvaluator();
    private readonly ILmaxReadOnlyRuntimeEvidenceSink _evidenceSink = evidenceSink ?? new LmaxReadOnlyRuntimeEvidenceSinkDisabled();
    private readonly ILmaxReadOnlyRuntimeRunStore _runStore = runStore ?? new LmaxReadOnlyRuntimeRunStoreNoOp();

    public Task<LmaxReadOnlyRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = _safetyGate.Evaluate(_options);
        var status = new LmaxReadOnlyRuntimeStatus(
            _options.ImplementationMode,
            _options.RequestedActivationLevel,
            safety.RunStatus,
            _options.Enabled,
            _options.ReadOnly,
            _options.AllowExternalConnections,
            _options.AllowCredentialUse,
            _options.AllowOrderSubmission,
            _options.PersistRawFixMessages,
            _options.PersistToTradingTables,
            _options.SubmitToShadowReplay,
            SchedulerEnabled: false,
            "LMAX read-only runtime adapter Phase 1 is inert: no sockets, credentials, evidence submission, scheduler, or trading-state mutation.",
            safety.Gates);

        return Task.FromResult(status);
    }

    public Task<LmaxReadOnlyRuntimeSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyRuntimeRunRequest? request = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_safetyGate.Evaluate(_options, request));

    public async Task<LmaxReadOnlyRuntimeRunResult> RunAsync(LmaxReadOnlyRuntimeRunRequest request, CancellationToken cancellationToken = default)
    {
        var safety = _safetyGate.Evaluate(_options, request);
        var sinkResult = await _evidenceSink.AcceptEvidenceBatchAsync(DisabledPreview(), cancellationToken);
        var message = string.Join(" ", [
            "LMAX read-only runtime adapter is disabled/inert for Phase 1.",
            safety.BlockedReason,
            sinkResult.Message,
            "No sockets were opened, no credentials were used, no orders were submitted, and no trading tables were written."
        ]);
        var result = new LmaxReadOnlyRuntimeRunResult(
            safety.RunStatus == LmaxReadOnlyRuntimeRunStatus.Disabled ? LmaxReadOnlyRuntimeRunStatus.Disabled : LmaxReadOnlyRuntimeRunStatus.Blocked,
            message,
            safety,
            null);

        await _runStore.RecordRunAttemptAsync(result, cancellationToken);
        return result;
    }

    private static LmaxReadOnlyRuntimeEvidenceBatchPreview DisabledPreview()
        => new(
            "disabled-phase-1",
            "lmax-fix-lifecycle-evidence-v1",
            "DisabledInertRuntime",
            DateTimeOffset.UtcNow,
            0,
            0,
            0,
            0,
            0,
            Sanitized: true,
            ContainsRawFix: false,
            ["Phase 1 does not create or submit runtime evidence."]);
}
