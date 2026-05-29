namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxTemporaryReadOnlyRuntimeBoundaryStatus
{
    NotAttempted,
    BlockedByPreflight,
    NotApplicableForInertValidation
}

public enum LmaxTemporaryReadOnlyRuntimeDecision
{
    InertScopeValid,
    Blocked
}

public sealed record LmaxReadOnlyRuntimeApprovedInstrument(
    string Symbol,
    string? SecurityId,
    string? SecurityIdSource,
    string ReadinessArchiveStatus,
    bool RequiresPreflightConfirmation,
    string? Caveat)
{
    public bool HasCaveat => !string.IsNullOrWhiteSpace(Caveat);
}

public static class LmaxReadOnlyRuntimeApprovedInstrumentAllowlist
{
    public const string UsdJpyCaveat = "prior failed-safe root cause remains unproven";

    public static IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument> Instruments { get; } =
    [
        new("GBPUSD", "4002", "8", "validated_archived", false, null),
        new("EURGBP", "4003", "8", "validated_archived", false, null),
        new("AUDUSD", "4007", "8", "validated_archived", false, null),
        new("USDJPY", "4004", "8", "validated_readiness_archive_with_caveat", false, UsdJpyCaveat)
    ];

    public static LmaxReadOnlyRuntimeApprovedInstrument? Find(string symbol)
        => Instruments.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
}

public sealed record LmaxReadOnlyRuntimeSafetyFlags(
    bool ProductionAccountRequested = false,
    bool AllowOrderSubmission = false,
    bool AllowLiveTrading = false,
    bool IsTradingEnabled = false,
    bool SchedulerEnabled = false,
    bool PollingEnabled = false,
    bool ReplayEnabled = false,
    bool ShadowReplayEnabled = false,
    bool TradingMutationEnabled = false,
    bool OrderGatewayRegistered = false,
    bool TradingGatewayRegistered = false,
    bool PersistentRuntimeEnablementRequested = false,
    bool DefaultGatewayRegistrationChangeRequested = false,
    bool OutputSanitizationEnabled = true);

public sealed record LmaxReadOnlyRuntimeOperatorApproval(
    string OperatorId,
    DateTimeOffset ApprovedAtUtc,
    string ApprovalPhrase,
    string ApprovedPhase,
    string Environment,
    IReadOnlyList<string> ApprovedInstruments);

public sealed record LmaxReadOnlyRuntimeShutdownRevertRecord(
    bool PlanPresent,
    bool ShutdownRequiredAfterAttempt,
    bool RevertRequiredAfterAttempt,
    string EvidencePath);

public sealed record LmaxReadOnlyRuntimeBoundaryEvidence(
    DateTimeOffset RecordedAtUtc,
    LmaxTemporaryReadOnlyRuntimeBoundaryStatus Tcp,
    LmaxTemporaryReadOnlyRuntimeBoundaryStatus Tls,
    LmaxTemporaryReadOnlyRuntimeBoundaryStatus FixLogon,
    LmaxTemporaryReadOnlyRuntimeBoundaryStatus MarketDataRequest,
    string SanitizedStatus,
    string? SanitizedErrorCategory);

public sealed record LmaxReadOnlyRuntimeForbiddenActionValidation(
    bool OrdersSubmitted,
    bool OrderPathEnabled,
    bool SchedulerStarted,
    bool PollingStarted,
    bool ReplayExecuted,
    bool ShadowReplaySubmitted,
    bool TradingStateMutated,
    bool ProductionAccountUsed)
{
    public bool Passed =>
        !OrdersSubmitted &&
        !OrderPathEnabled &&
        !SchedulerStarted &&
        !PollingStarted &&
        !ReplayExecuted &&
        !ShadowReplaySubmitted &&
        !TradingStateMutated &&
        !ProductionAccountUsed;
}

public sealed record LmaxReadOnlyRuntimeNonMutationValidation(
    bool TradingStateMutated,
    bool PostEndpointInvoked,
    bool RuntimePoweredUp,
    bool CredentialsLoaded,
    bool CredentialsPrinted,
    bool CredentialsStored)
{
    public bool Passed =>
        !TradingStateMutated &&
        !PostEndpointInvoked &&
        !RuntimePoweredUp &&
        !CredentialsLoaded &&
        !CredentialsPrinted &&
        !CredentialsStored;
}

public sealed record LmaxReadOnlyRuntimeRailIsolationValidation(
    bool ValidatedRailsModified,
    bool Phase7ArchiveModified,
    bool UsdJpyT1T7ArtifactsModified,
    bool NonApprovedInstrumentTouched)
{
    public bool Passed =>
        !ValidatedRailsModified &&
        !Phase7ArchiveModified &&
        !UsdJpyT1T7ArtifactsModified &&
        !NonApprovedInstrumentTouched;
}

public sealed record LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
    string Symbol,
    string? SecurityId,
    string? SecurityIdSource,
    string EnvironmentLabel,
    LmaxTemporaryReadOnlyRuntimeBoundaryStatus BoundaryStatus,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    DateTimeOffset RecordedAtUtc,
    string? Caveat)
{
    public int MarketDataSnapshotCount { get; init; }

    public int MarketDataRequestRejectCount { get; init; }

    public int BusinessMessageRejectCount { get; init; }

    public int SessionRejectCount { get; init; }

    public string? SanitizedReasonCategory { get; init; }
}

public sealed record LmaxTemporaryReadOnlyRuntimeActivationScope(
    string Phase,
    string Environment,
    bool DemoReadOnly,
    bool Temporary,
    bool InertValidatorOnly,
    IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument> Instruments,
    LmaxReadOnlyRuntimeSafetyFlags SafetyFlags,
    LmaxReadOnlyRuntimeOperatorApproval? OperatorApproval,
    LmaxReadOnlyRuntimeShutdownRevertRecord? ShutdownRevert,
    int MaxRuntimeSeconds,
    string OutputRoot);

public sealed record LmaxReadOnlyRuntimePreflightIssue(string Code, string Path, string Message);

public sealed record LmaxReadOnlyRuntimePreflightGate(
    LmaxTemporaryReadOnlyRuntimeDecision Decision,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues)
{
    public bool Passed => Decision == LmaxTemporaryReadOnlyRuntimeDecision.InertScopeValid && Issues.Count == 0;
}

public static class LmaxTemporaryReadOnlyRuntimeActivationValidator
{
    private static readonly StringComparer SymbolComparer = StringComparer.OrdinalIgnoreCase;

    public static LmaxReadOnlyRuntimePreflightGate Validate(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();

        if (scope.OperatorApproval is null)
        {
            Add(issues, "OperatorApprovalMissing", "$.operatorApproval", "Operator approval is required even for inert validation.");
        }

        if (!scope.DemoReadOnly || !string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "EnvironmentNotDemoReadOnly", "$.environment", "Environment must be Demo/read-only.");
        }

        if (!scope.Temporary)
        {
            Add(issues, "ScopeNotTemporary", "$.temporary", "Activation scope must be temporary.");
        }

        if (!scope.InertValidatorOnly)
        {
            Add(issues, "ScopeNotInertValidatorOnly", "$.inertValidatorOnly", "R5 scope must remain inert and validator-only.");
        }

        if (scope.MaxRuntimeSeconds <= 0 || scope.MaxRuntimeSeconds > LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds)
        {
            Add(issues, "InvalidTimebox", "$.maxRuntimeSeconds", $"Max runtime seconds must be between 1 and {LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds}.");
        }

        if (string.IsNullOrWhiteSpace(scope.OutputRoot) ||
            !scope.OutputRoot.Replace('\\', '/').StartsWith("artifacts/readiness/lmax-runtime-enablement", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "InvalidOutputRoot", "$.outputRoot", "Output root must stay under artifacts/readiness/lmax-runtime-enablement.");
        }

        ValidateInstruments(scope.Instruments, issues);
        ValidateApproval(scope, issues);
        ValidateSafetyFlags(scope.SafetyFlags, issues);

        if (scope.ShutdownRevert is null || !scope.ShutdownRevert.PlanPresent)
        {
            Add(issues, "ShutdownRevertPlanMissing", "$.shutdownRevert", "Shutdown/revert plan is required before any future activation can be considered.");
        }

        return new LmaxReadOnlyRuntimePreflightGate(
            issues.Count == 0 ? LmaxTemporaryReadOnlyRuntimeDecision.InertScopeValid : LmaxTemporaryReadOnlyRuntimeDecision.Blocked,
            issues);
    }

    private static void ValidateInstruments(IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument> instruments, List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (instruments.Count == 0)
        {
            Add(issues, "InstrumentListMissing", "$.instruments", "At least one approved instrument is required.");
            return;
        }

        var approved = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;
        var approvedSymbols = approved.Select(x => x.Symbol).ToHashSet(SymbolComparer);
        var seen = new HashSet<string>(SymbolComparer);

        foreach (var instrument in instruments)
        {
            if (!approvedSymbols.Contains(instrument.Symbol))
            {
                Add(issues, "InstrumentNotApproved", "$.instruments", $"Instrument '{instrument.Symbol}' is not in the approved read-only runtime allowlist.");
                continue;
            }

            if (!seen.Add(instrument.Symbol))
            {
                Add(issues, "DuplicateInstrument", "$.instruments", $"Instrument '{instrument.Symbol}' appears more than once.");
            }

            var expected = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol);
            if (expected is not null && !string.Equals(expected.SecurityId, instrument.SecurityId, StringComparison.Ordinal))
            {
                Add(issues, "SecurityIdMismatch", "$.instruments", $"Instrument '{instrument.Symbol}' SecurityID must match approved evidence or require later preflight confirmation.");
            }

            if (expected is not null && !string.Equals(expected.SecurityIdSource, instrument.SecurityIdSource, StringComparison.Ordinal))
            {
                Add(issues, "SecurityIdSourceMismatch", "$.instruments", $"Instrument '{instrument.Symbol}' SecurityIDSource must match approved evidence or require later preflight confirmation.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "UsdJpyCaveatMissing", "$.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
            }
        }
    }

    private static void ValidateApproval(LmaxTemporaryReadOnlyRuntimeActivationScope scope, List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (scope.OperatorApproval is null)
        {
            return;
        }

        if (!string.Equals(scope.OperatorApproval.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "ApprovalEnvironmentInvalid", "$.operatorApproval.environment", "Approval environment must be Demo/read-only.");
        }

        var requestedSymbols = scope.Instruments.Select(x => x.Symbol).OrderBy(x => x, SymbolComparer).ToArray();
        var approvedSymbols = scope.OperatorApproval.ApprovedInstruments.OrderBy(x => x, SymbolComparer).ToArray();
        if (!requestedSymbols.SequenceEqual(approvedSymbols, SymbolComparer))
        {
            Add(issues, "ApprovalInstrumentMismatch", "$.operatorApproval.approvedInstruments", "Operator approval must match the requested instrument scope exactly.");
        }

        if (string.IsNullOrWhiteSpace(scope.OperatorApproval.ApprovalPhrase))
        {
            Add(issues, "ApprovalPhraseMissing", "$.operatorApproval.approvalPhrase", "Approval phrase is required.");
        }
    }

    private static void ValidateSafetyFlags(LmaxReadOnlyRuntimeSafetyFlags flags, List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        FailIf(flags.ProductionAccountRequested, issues, "ProductionAccountRequested", "$.safetyFlags.productionAccountRequested", "Production account is forbidden.");
        FailIf(flags.AllowOrderSubmission, issues, "AllowOrderSubmission", "$.safetyFlags.allowOrderSubmission", "Order submission must remain disabled.");
        FailIf(flags.AllowLiveTrading, issues, "AllowLiveTrading", "$.safetyFlags.allowLiveTrading", "Live trading must remain disabled.");
        FailIf(flags.IsTradingEnabled, issues, "IsTradingEnabled", "$.safetyFlags.isTradingEnabled", "Trading must remain disabled.");
        FailIf(flags.SchedulerEnabled, issues, "SchedulerEnabled", "$.safetyFlags.schedulerEnabled", "Scheduler must remain disabled.");
        FailIf(flags.PollingEnabled, issues, "PollingEnabled", "$.safetyFlags.pollingEnabled", "Polling must remain disabled.");
        FailIf(flags.ReplayEnabled, issues, "ReplayEnabled", "$.safetyFlags.replayEnabled", "Replay must remain disabled.");
        FailIf(flags.ShadowReplayEnabled, issues, "ShadowReplayEnabled", "$.safetyFlags.shadowReplayEnabled", "Shadow replay submit must remain disabled.");
        FailIf(flags.TradingMutationEnabled, issues, "TradingMutationEnabled", "$.safetyFlags.tradingMutationEnabled", "Trading mutation must remain disabled.");
        FailIf(flags.OrderGatewayRegistered, issues, "OrderGatewayRegistered", "$.safetyFlags.orderGatewayRegistered", "Order gateway registration is forbidden.");
        FailIf(flags.TradingGatewayRegistered, issues, "TradingGatewayRegistered", "$.safetyFlags.tradingGatewayRegistered", "Trading gateway registration is forbidden.");
        FailIf(flags.PersistentRuntimeEnablementRequested, issues, "PersistentRuntimeEnablementRequested", "$.safetyFlags.persistentRuntimeEnablementRequested", "Persistent runtime enablement is forbidden.");
        FailIf(flags.DefaultGatewayRegistrationChangeRequested, issues, "DefaultGatewayRegistrationChangeRequested", "$.safetyFlags.defaultGatewayRegistrationChangeRequested", "Default gateway registration changes are forbidden.");

        if (!flags.OutputSanitizationEnabled)
        {
            Add(issues, "OutputSanitizationDisabled", "$.safetyFlags.outputSanitizationEnabled", "Output sanitization must be enabled.");
        }
    }

    private static void FailIf(bool condition, List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
    {
        if (condition)
        {
            Add(issues, code, path, message);
        }
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
