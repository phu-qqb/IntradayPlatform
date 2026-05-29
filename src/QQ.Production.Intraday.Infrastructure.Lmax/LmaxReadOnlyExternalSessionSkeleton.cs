namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyExternalSessionSkeletonSafetyReport(
    string ExternalSessionImplementationMode,
    bool SocketActivation,
    bool FixLogonImplemented,
    bool CredentialUseImplemented,
    bool OrderSubmissionImplemented,
    bool ShadowReplaySubmitImplemented,
    bool TradingMutationImplemented,
    bool SchedulerImplemented,
    bool RuntimeGatewayRegistrationImplemented,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates);

public sealed class LmaxReadOnlyExternalSessionSkeleton(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyExternalSession
{
    private static readonly LmaxReadOnlyExternalSessionCounters EmptyCounters = new(0, 0, 0, 0, 0, 0);

    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();

    public Task<LmaxReadOnlyExternalSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = EvaluateSkeletonSafety(new LmaxReadOnlyExternalSessionRequest("status check"));
        return Task.FromResult(new LmaxReadOnlyExternalSessionStatus(
            safety.RunStatus,
            _options.ImplementationMode,
            _options.RequestedActivationLevel,
            ExternalSessionImplementationAvailable: false,
            SocketImplementationAvailable: false,
            ReadOnly: true,
            "LMAX read-only external session Phase 4E skeleton is present but disabled. ExternalSessionImplementationMode=SkeletonOnly; SocketActivation=false; FixLogonImplemented=false; CredentialUseImplemented=false; OrderSubmissionImplemented=false; ShadowReplaySubmitImplemented=false; TradingMutationImplemented=false.",
            EmptyCounters,
            safety.Gates));
    }

    public Task<LmaxReadOnlyExternalSessionSafetyEvaluation> EvaluateSafetyAsync(
        LmaxReadOnlyExternalSessionRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EvaluateSkeletonSafety(request));

    public Task<LmaxReadOnlyExternalSessionResult> RunAsync(
        LmaxReadOnlyExternalSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var safety = EvaluateSkeletonSafety(request);
        var status = safety.RunStatus == LmaxReadOnlyRuntimeRunStatus.Disabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : LmaxReadOnlyRuntimeRunStatus.Blocked;
        return Task.FromResult(new LmaxReadOnlyExternalSessionResult(
            status,
            "LMAX read-only external session Phase 4E skeleton is blocked: ExternalSessionSkeletonOnly. No sockets were opened, no FIX logon/logout exists, no credentials were used, no evidence was created, no shadow replay submit occurred, and no trading state was mutated. " + safety.BlockedReason,
            ExternalSessionImplementationAvailable: false,
            SocketOpened: false,
            CredentialsUsed: false,
            EvidenceCreated: false,
            SubmittedToShadowReplay: false,
            EmptyCounters,
            safety));
    }

    public LmaxReadOnlyExternalSessionSkeletonSafetyReport GetSkeletonSafetyReport(
        LmaxReadOnlyExternalSessionRequest? request = null)
    {
        var safety = EvaluateSkeletonSafety(request ?? new LmaxReadOnlyExternalSessionRequest("skeleton safety report"));
        return new LmaxReadOnlyExternalSessionSkeletonSafetyReport(
            "SkeletonOnly",
            SocketActivation: false,
            FixLogonImplemented: false,
            CredentialUseImplemented: false,
            OrderSubmissionImplemented: false,
            ShadowReplaySubmitImplemented: false,
            TradingMutationImplemented: false,
            SchedulerImplemented: false,
            RuntimeGatewayRegistrationImplemented: false,
            safety.Gates);
    }

    private LmaxReadOnlyExternalSessionSafetyEvaluation EvaluateSkeletonSafety(
        LmaxReadOnlyExternalSessionRequest request)
    {
        var runtimeRequest = new LmaxReadOnlyRuntimeRunRequest(
            request.Reason,
            request.MaxEvents,
            request.MaxRuntimeSeconds,
            DryRun: true,
            request.RequestedActivationLevel);
        var runtimeEvaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(_options, runtimeRequest);
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>(runtimeEvaluation.Gates)
        {
            Gate("ExternalSessionSkeletonPresent", true, "true", "true - Phase 4E skeleton boundary is present."),
            Gate("ExternalSessionImplementationStarted", false, "false in Phase 4E", "false - real external implementation is not started."),
            Gate("GuardedTransportInterfacePresent", true, "true", "true - Phase 4F guarded transport boundary exists."),
            Gate("GuardedTransportImplementationDisabled", false, "false until a separate implementation gate", "true - guarded transport is disabled."),
            Gate("NetworkTransportImplemented", true, "false in Phase 4F", "false - no network transport implementation exists."),
            Gate("SocketActivationAllowed", false, "false in Phase 4E", "false - socket activation is not implemented or allowed."),
            Gate("FixLogonAllowed", false, "false in Phase 4E", "false - FIX logon/logout is not implemented or allowed."),
            Gate("CredentialUseAllowed", false, "false in Phase 4E", "false - credential use is not implemented or allowed."),
            Gate("OrderSubmissionAllowed", false, "false", "false - order submission is forbidden."),
            Gate("ShadowReplaySubmitAllowed", false, "false in Phase 4E", "false - runtime shadow replay submit remains deferred."),
            Gate("TradingMutationAllowed", false, "false", "false - trading-state mutation is forbidden."),
            Gate("SchedulerAllowed", false, "false", "false - scheduler auto-run is forbidden."),
            Gate("RuntimeGatewayRegistrationAllowed", false, "false", "false - API/Worker execution gateway remains FakeLmaxGateway.")
        };

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !_options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "Skeleton gates passed, but Phase 4E still does not provide a runnable external implementation."
            : "Blocked by external-session skeleton gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyExternalSessionSafetyEvaluation(status, reason, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}

public sealed class LmaxReadOnlyExternalSessionSkeletonFactory(
    LmaxReadOnlyRuntimeAdapterOptions? options = null) : ILmaxReadOnlyExternalSessionFactory
{
    public ILmaxReadOnlyExternalSession CreateDisabledSession()
        => new LmaxReadOnlyExternalSessionSkeleton(options);

    public ILmaxReadOnlyExternalSession CreateSkeletonSession()
        => new LmaxReadOnlyExternalSessionSkeleton(options);
}
