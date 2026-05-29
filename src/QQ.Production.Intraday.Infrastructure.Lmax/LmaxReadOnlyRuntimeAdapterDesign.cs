namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyRuntimeImplementationMode
{
    DesignOnly,
    DisabledNoOp,
    FakeInMemory,
    FutureReadOnly
}

public enum LmaxReadOnlyRuntimeActivationLevel
{
    Level0DesignOnly = 0,
    Level1DisabledSkeleton = 1,
    Level2LocalManualNoExternal = 2,
    Level3LabExternalCaptureToFile = 3,
    Level4RuntimeManualReadOnlyConnectionNoReplaySubmit = 4,
    Level5RuntimeManualReadOnlyConnectionWithReplaySubmit = 5,
    Level6ScheduledReadOnlyShadow = 6,
    Level7FutureTradingAdapterDesign = 7
}

public enum LmaxReadOnlyRuntimeSafetyGateStatus
{
    Passed,
    Failed,
    Warning,
    Informational
}

public enum LmaxReadOnlyRuntimeRunStatus
{
    Disabled,
    Blocked,
    DesignOnly,
    DryRun,
    Completed,
    Failed
}

public enum LmaxReadOnlyRuntimeSourceEventType
{
    MarketDataSnapshot,
    ExecutionReport,
    OrderStatus,
    TradeCaptureReport,
    ProtocolReject,
    Heartbeat,
    TestRequest,
    Logout,
    Unknown
}

public sealed record LmaxReadOnlyRuntimeAdapterOptions
{
    public const int SafeMaxEventsPerRun = 1_000;
    public const int SafeMaxRuntimeSeconds = 300;

    public bool Enabled { get; init; }
    public LmaxReadOnlyRuntimeImplementationMode ImplementationMode { get; init; } = LmaxReadOnlyRuntimeImplementationMode.DesignOnly;
    public bool AllowExternalConnections { get; init; }
    public bool AllowCredentialUse { get; init; }
    public bool ReadOnly { get; init; } = true;
    public bool AllowOrderSubmission { get; init; }
    public bool PersistRawFixMessages { get; init; }
    public bool PersistToTradingTables { get; init; }
    public bool SubmitToShadowReplay { get; init; }
    public bool SchedulerEnabled { get; init; }
    public string? FixtureEvidenceFile { get; init; }
    public int MaxEventsPerRun { get; init; } = 100;
    public int MaxRuntimeSeconds { get; init; } = 30;
    public string EnvironmentName { get; init; } = "Local";
    public bool RequireOperationalReadinessPass { get; init; } = true;
    public bool OperationalReadinessPassed { get; init; }
    public bool RequireGovernanceApproval { get; init; } = true;
    public bool GovernanceApproved { get; init; }
    public bool RequireLocalOnlyApi { get; init; } = true;
    public bool LocalOnlyApi { get; init; } = true;
    public bool DryRun { get; init; } = true;
    public LmaxReadOnlyRuntimeActivationLevel RequestedActivationLevel { get; init; } = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton;
    public LmaxReadOnlyRuntimeActivationLevel MaxAllowedActivationLevel { get; init; } = LmaxReadOnlyRuntimeActivationLevel.Level3LabExternalCaptureToFile;
}

public sealed record LmaxReadOnlyRuntimeRunRequest(
    string? Reason,
    int? MaxEvents = null,
    int? MaxRuntimeSeconds = null,
    bool? DryRun = null,
    LmaxReadOnlyRuntimeActivationLevel? RequestedActivationLevel = null);

public sealed record LmaxReadOnlyRuntimeSafetyGateResult(
    string Name,
    LmaxReadOnlyRuntimeSafetyGateStatus Status,
    string ObservedValue,
    string ExpectedSafeValue,
    string Message)
{
    public bool BlocksRun => Status == LmaxReadOnlyRuntimeSafetyGateStatus.Failed;
}

public sealed record LmaxReadOnlyRuntimeSafetyEvaluation(
    LmaxReadOnlyRuntimeRunStatus RunStatus,
    string BlockedReason,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates)
{
    public bool Passed => !Gates.Any(x => x.BlocksRun);
    public IReadOnlyList<string> FailedGateNames => Gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
}

public sealed record LmaxReadOnlyRuntimeEventEnvelope(
    string EventId,
    LmaxReadOnlyRuntimeSourceEventType SourceEventType,
    DateTimeOffset ObservedAtUtc,
    string? ClientOrderId,
    string? BrokerOrderId,
    string? BrokerExecutionId,
    string? InstrumentId,
    string? Symbol,
    string SanitizedPayloadJson);

public sealed record LmaxReadOnlyRuntimeEvidenceBatchSummary(
    string BatchId,
    string EvidenceMode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int InputEventCount,
    int UniqueEventCount,
    int DuplicateEventCount,
    bool SubmittedToShadowReplay,
    IReadOnlyList<string> Warnings);

public sealed record LmaxReadOnlyRuntimeRunResult(
    LmaxReadOnlyRuntimeRunStatus Status,
    string Message,
    LmaxReadOnlyRuntimeSafetyEvaluation Safety,
    LmaxReadOnlyRuntimeEvidenceBatchSummary? BatchSummary)
{
    public string? RunId { get; init; }
    public LmaxReadOnlyRuntimeRunMode RunMode { get; init; } = LmaxReadOnlyRuntimeRunMode.DisabledDryRun;
    public string? FixtureEvidenceFile { get; init; }
    public string? EvidenceMode { get; init; }
    public int ExecutionReportCount { get; init; }
    public int OrderStatusCount { get; init; }
    public int TradeCaptureReportCount { get; init; }
    public int ProtocolRejectCount { get; init; }
    public int MarketDataSnapshotCount { get; init; }
    public int InputEventCount { get; init; }
    public int ValidationErrorCount { get; init; }
    public int ValidationWarningCount { get; init; }
    public int ValidationInfoCount { get; init; }
    public int ObservationCount { get; init; }
    public int BlockingObservationCount { get; init; }
    public int WarningObservationCount { get; init; }
    public string? ReplayRunId { get; init; }
}

public static class LmaxReadOnlyRuntimeSafetyGate
{
    public static LmaxReadOnlyRuntimeSafetyEvaluation Evaluate(
        LmaxReadOnlyRuntimeAdapterOptions options,
        LmaxReadOnlyRuntimeRunRequest? request = null)
    {
        var requestedLevel = request?.RequestedActivationLevel ?? options.RequestedActivationLevel;
        var dryRun = request?.DryRun ?? options.DryRun;
        var maxEvents = request?.MaxEvents ?? options.MaxEventsPerRun;
        var maxRuntimeSeconds = request?.MaxRuntimeSeconds ?? options.MaxRuntimeSeconds;
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>
        {
            Gate("Enabled", options.Enabled, "true for any future runtime reader run", options.Enabled ? "Reader is explicitly enabled." : "Reader is disabled by default."),
            Gate("ImplementationMode", options.ImplementationMode != LmaxReadOnlyRuntimeImplementationMode.DesignOnly, "DisabledNoOp/FakeInMemory/FutureReadOnly after a future gate", $"Implementation mode is {options.ImplementationMode}; design-only mode cannot execute."),
            Gate("ReadOnly", options.ReadOnly, "true", options.ReadOnly ? "Reader is read-only." : "ReadOnly=false is rejected."),
            Gate("AllowOrderSubmission", !options.AllowOrderSubmission, "false", options.AllowOrderSubmission ? "Order submission is forbidden for the read-only runtime reader." : "Order submission remains disabled."),
            Gate("PersistToTradingTables", !options.PersistToTradingTables, "false", options.PersistToTradingTables ? "Trading-table persistence is forbidden for shadow reading." : "No trading-table persistence requested."),
            Gate("PersistRawFixMessages", !options.PersistRawFixMessages, "false", options.PersistRawFixMessages ? "Raw FIX persistence is blocked until a separate sanitized-retention design gate." : "Raw FIX persistence remains disabled."),
            Gate("SchedulerEnabled", !options.SchedulerEnabled, "false", options.SchedulerEnabled ? "Scheduler auto-run is forbidden for the read-only runtime adapter." : "Scheduler auto-run remains disabled."),
            Gate("DryRun", dryRun, "true for this design gate", dryRun ? "DryRun remains true." : "DryRun=false is blocked until a future activation gate."),
            Gate("AllowExternalConnections", options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory ? !options.AllowExternalConnections : options.AllowExternalConnections, options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory ? "false for fixture-only fake adapter" : "true only after a future explicit runtime gate", options.AllowExternalConnections ? "External connections were explicitly requested." : "External connections are disabled."),
            Gate("AllowCredentialUse", options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory ? !options.AllowCredentialUse : options.AllowCredentialUse, options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory ? "false for fixture-only fake adapter" : "true only after a future explicit credential gate", options.AllowCredentialUse ? "Credential use was explicitly requested." : "Credential use is disabled."),
            Gate("Production", !IsProduction(options.EnvironmentName), "non-Production", $"Environment is '{options.EnvironmentName}'."),
            Gate("OperationalReadiness", !options.RequireOperationalReadinessPass || options.OperationalReadinessPassed, "readiness gate passed", options.OperationalReadinessPassed ? "Operational readiness is marked passed." : "Operational readiness pass is required before activation."),
            Gate("GovernanceApproval", !options.RequireGovernanceApproval || options.GovernanceApproved || requestedLevel <= LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton, "governance approved before activation", options.GovernanceApproved ? "Governance approval is marked present." : "Governance approval is required before activation beyond disabled skeleton."),
            Gate("LocalOnlyApi", !options.RequireLocalOnlyApi || options.LocalOnlyApi, "local-only API submission path", options.LocalOnlyApi ? "API target is local-only." : "Non-local API target is blocked for runtime shadow submission."),
            Gate("MaxEventsPerRun", maxEvents > 0 && maxEvents <= LmaxReadOnlyRuntimeAdapterOptions.SafeMaxEventsPerRun, $"1..{LmaxReadOnlyRuntimeAdapterOptions.SafeMaxEventsPerRun}", maxEvents.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("MaxRuntimeSeconds", maxRuntimeSeconds > 0 && maxRuntimeSeconds <= LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds, $"1..{LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds}", maxRuntimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("ActivationLevel", requestedLevel <= options.MaxAllowedActivationLevel && requestedLevel < LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit, $"<= {options.MaxAllowedActivationLevel} and below runtime connection levels", requestedLevel.ToString())
        };

        if (options.SubmitToShadowReplay)
        {
            gates.Add(Gate("SubmitToShadowReplay", false, "false until runtime reader activation gate", "Submitting directly to shadow replay from a runtime reader is not enabled by this design."));
        }
        else
        {
            gates.Add(new LmaxReadOnlyRuntimeSafetyGateResult("SubmitToShadowReplay", LmaxReadOnlyRuntimeSafetyGateStatus.Informational, "false", "false", "Shadow replay submission remains disabled for this design-only runtime reader."));
        }

        if (requestedLevel == LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit)
        {
            gates.Add(Gate("Phase4EnvironmentName", string.Equals(options.EnvironmentName, "Demo", StringComparison.OrdinalIgnoreCase), "Demo", $"Environment is '{options.EnvironmentName}'."));
            gates.Add(Gate("Phase4ReasonRequired", !string.IsNullOrWhiteSpace(request?.Reason), "non-empty manual reason", string.IsNullOrWhiteSpace(request?.Reason) ? "Reason is missing." : "Reason is present."));
            gates.Add(Gate("Phase4ImplementationNotStarted", false, "external read-only session implementation present after separate approval", "Phase 4 preflight has not implemented sockets, credentials, or an external read-only session."));
        }

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "All current design gates passed; no runtime connectivity is implemented by this contract."
            : "Blocked by safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyRuntimeSafetyEvaluation(status, reason, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observedOrMessage)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observedOrMessage, expected, observedOrMessage);

    private static bool IsProduction(string? environment)
        => string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)
           || string.Equals(environment, "Prod", StringComparison.OrdinalIgnoreCase);
}
