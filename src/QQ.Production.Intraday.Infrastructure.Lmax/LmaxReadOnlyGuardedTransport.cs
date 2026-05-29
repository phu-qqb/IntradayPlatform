namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlyGuardedTransport
{
    Task<LmaxReadOnlyGuardedTransportStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyGuardedTransportSafetyReport> EvaluateSafetyAsync(LmaxReadOnlyGuardedTransportRequest request, CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyGuardedTransportResult> ConnectReadOnlyAsync(LmaxReadOnlyGuardedTransportRequest request, CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyGuardedTransportResult> ReadEventsAsync(LmaxReadOnlyGuardedTransportRequest request, CancellationToken cancellationToken = default);
    Task<LmaxReadOnlyGuardedTransportResult> DisconnectAsync(LmaxReadOnlyGuardedTransportRequest request, CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyGuardedTransportFactory
{
    ILmaxReadOnlyGuardedTransport CreateDisabledTransport();
}

public sealed record LmaxReadOnlyGuardedTransportRequest(
    string? Reason,
    int? MaxEvents = null,
    int? MaxRuntimeSeconds = null,
    LmaxReadOnlyRuntimeActivationLevel RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit);

public sealed record LmaxReadOnlyGuardedTransportCapabilities(
    bool NetworkTransportImplemented,
    bool SocketActivation,
    bool FixLogonImplemented,
    bool CredentialUseImplemented,
    bool OrderSubmissionImplemented,
    bool ReadOnlyOnly,
    bool ShadowReplaySubmitImplemented,
    bool TradingMutationImplemented,
    bool SchedulerImplemented);

public sealed record LmaxReadOnlyGuardedTransportStatus(
    LmaxReadOnlyRuntimeRunStatus Status,
    string ImplementationMode,
    bool Disabled,
    LmaxReadOnlyGuardedTransportCapabilities Capabilities,
    string Message,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> SafetyGates);

public sealed record LmaxReadOnlyGuardedTransportResult(
    LmaxReadOnlyRuntimeRunStatus Status,
    string Operation,
    bool NetworkTransportImplemented,
    bool SocketOpened,
    bool FixLogonAttempted,
    bool CredentialsUsed,
    bool EventsRead,
    IReadOnlyList<LmaxReadOnlyExternalSessionEvent> Events,
    string Message,
    LmaxReadOnlyGuardedTransportSafetyReport Safety);

public sealed record LmaxReadOnlyGuardedTransportSafetyReport(
    LmaxReadOnlyRuntimeRunStatus RunStatus,
    string BlockedReason,
    LmaxReadOnlyGuardedTransportCapabilities Capabilities,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates)
{
    public bool Passed => !Gates.Any(x => x.BlocksRun);
    public IReadOnlyList<string> FailedGateNames => Gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
}

public sealed class LmaxReadOnlyGuardedTransportDisabled(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyGuardedTransport
{
    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();

    public Task<LmaxReadOnlyGuardedTransportStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = Evaluate(new LmaxReadOnlyGuardedTransportRequest("status check"));
        return Task.FromResult(new LmaxReadOnlyGuardedTransportStatus(
            safety.RunStatus,
            "DisabledGuardedTransport",
            Disabled: true,
            safety.Capabilities,
            "LMAX read-only guarded transport Phase 4F is an interface boundary with a disabled implementation only. NetworkTransportImplemented=false; SocketActivation=false; FixLogonImplemented=false; CredentialUseImplemented=false; OrderSubmissionImplemented=false; ReadOnlyOnly=true.",
            safety.Gates));
    }

    public Task<LmaxReadOnlyGuardedTransportSafetyReport> EvaluateSafetyAsync(
        LmaxReadOnlyGuardedTransportRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Evaluate(request));

    public Task<LmaxReadOnlyGuardedTransportResult> ConnectReadOnlyAsync(
        LmaxReadOnlyGuardedTransportRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BlockedResult("ConnectReadOnly", request));

    public Task<LmaxReadOnlyGuardedTransportResult> ReadEventsAsync(
        LmaxReadOnlyGuardedTransportRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BlockedResult("ReadEvents", request));

    public Task<LmaxReadOnlyGuardedTransportResult> DisconnectAsync(
        LmaxReadOnlyGuardedTransportRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BlockedResult("Disconnect", request));

    private LmaxReadOnlyGuardedTransportResult BlockedResult(string operation, LmaxReadOnlyGuardedTransportRequest request)
    {
        var safety = Evaluate(request);
        var status = safety.RunStatus == LmaxReadOnlyRuntimeRunStatus.Disabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : LmaxReadOnlyRuntimeRunStatus.Blocked;
        return new LmaxReadOnlyGuardedTransportResult(
            status,
            operation,
            NetworkTransportImplemented: false,
            SocketOpened: false,
            FixLogonAttempted: false,
            CredentialsUsed: false,
            EventsRead: false,
            Events: [],
            "LMAX read-only guarded transport is disabled/not implemented for Phase 4F. No network transport exists, no socket was opened, no FIX logon was attempted, no credentials were used, no events were read, no shadow replay submit occurred, and no trading state was mutated. " + safety.BlockedReason,
            safety);
    }

    private LmaxReadOnlyGuardedTransportSafetyReport Evaluate(LmaxReadOnlyGuardedTransportRequest request)
    {
        var runtimeRequest = new LmaxReadOnlyRuntimeRunRequest(
            request.Reason,
            request.MaxEvents,
            request.MaxRuntimeSeconds,
            DryRun: true,
            request.RequestedActivationLevel);
        var runtimeEvaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(_options, runtimeRequest);
        var capabilities = new LmaxReadOnlyGuardedTransportCapabilities(
            NetworkTransportImplemented: false,
            SocketActivation: false,
            FixLogonImplemented: false,
            CredentialUseImplemented: false,
            OrderSubmissionImplemented: false,
            ReadOnlyOnly: true,
            ShadowReplaySubmitImplemented: false,
            TradingMutationImplemented: false,
            SchedulerImplemented: false);
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>(runtimeEvaluation.Gates)
        {
            Gate("GuardedTransportInterfacePresent", true, "true", "true - guarded transport contract exists."),
            Gate("GuardedTransportImplementationDisabled", false, "false until a separate implementation gate", "true - disabled transport is the only implementation."),
            Gate("NetworkTransportImplemented", !capabilities.NetworkTransportImplemented, "false in Phase 4F", "false - no network transport implementation exists."),
            Gate("SocketActivationAllowed", false, "false in Phase 4F", "false - socket activation is not implemented or allowed."),
            Gate("FixLogonAllowed", false, "false in Phase 4F", "false - FIX logon/logout is not implemented or allowed."),
            Gate("CredentialUseAllowed", false, "false in Phase 4F", "false - credential use is not implemented or allowed."),
            Gate("OrderSubmissionAllowed", false, "false", "false - order submission is forbidden."),
            Gate("RuntimeReadOnlyOnly", capabilities.ReadOnlyOnly, "true", "true - transport contract is read-only only."),
            Gate("ShadowReplaySubmitAllowed", false, "false in Phase 4F", "false - runtime shadow replay submit remains deferred."),
            Gate("TradingMutationAllowed", false, "false", "false - trading-state mutation is forbidden."),
            Gate("SchedulerAllowed", false, "false", "false - scheduler auto-run is forbidden."),
            Gate("Phase4FStillNoSocket", true, "true", "true - Phase 4F still has no socket implementation.")
        };

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !_options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "Guarded transport gates passed, but the disabled transport still has no runnable implementation."
            : "Blocked by guarded transport safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyGuardedTransportSafetyReport(status, reason, capabilities, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}

public sealed class LmaxReadOnlyGuardedTransportDisabledFactory(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyGuardedTransportFactory
{
    public ILmaxReadOnlyGuardedTransport CreateDisabledTransport()
        => new LmaxReadOnlyGuardedTransportDisabled(options);
}
