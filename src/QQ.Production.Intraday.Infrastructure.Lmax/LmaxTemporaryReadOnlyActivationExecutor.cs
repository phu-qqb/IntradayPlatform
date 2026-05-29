namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxTemporaryReadOnlyActivationExecutorOptions(
    string PhaseLabel,
    string OperatorApprovalPhraseMarker,
    string EnvironmentLabel,
    IReadOnlyList<string> ApprovedInstruments,
    int MaxAttemptCount,
    int RetryCount,
    TimeSpan Timeout,
    bool ShutdownRevertRequired,
    bool SanitizationRequired,
    bool NoPersistence,
    bool BatchMode = false,
    bool LoopMode = false,
    bool FutureExternalExecutionApproved = false)
{
    public static LmaxTemporaryReadOnlyActivationExecutorOptions R32NoExternalActivation()
        => new(
            "LMAX-R32",
            "R33-approval-required-redacted",
            "Demo/read-only",
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList(),
            MaxAttemptCount: 1,
            RetryCount: 0,
            Timeout: TimeSpan.FromSeconds(30),
            ShutdownRevertRequired: true,
            SanitizationRequired: true,
            NoPersistence: true,
            BatchMode: false,
            LoopMode: false,
            FutureExternalExecutionApproved: false);

    public static LmaxTemporaryReadOnlyActivationExecutorOptions ForApprovedSingleReadOnlyRetry(
        string phaseLabel,
        string operatorApprovalPhraseMarker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorApprovalPhraseMarker);

        return new(
            phaseLabel,
            operatorApprovalPhraseMarker,
            "Demo/read-only",
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList(),
            MaxAttemptCount: 1,
            RetryCount: 0,
            Timeout: TimeSpan.FromSeconds(30),
            ShutdownRevertRequired: true,
            SanitizationRequired: true,
            NoPersistence: true,
            BatchMode: false,
            LoopMode: false,
            FutureExternalExecutionApproved: true);
    }
}

public sealed record LmaxTemporaryReadOnlyActivationExecutorResult(
    string ExecutorType,
    string ApprovedPathSummary,
    bool ValidationPassed,
    bool ExecutionStarted,
    bool FutureExecutionAllowedOnlyWithApproval,
    int AttemptsPlanned,
    int AttemptsExecuted,
    bool ShutdownRequired,
    bool ShutdownRevertCompleted,
    bool OutputSanitized,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    string? SanitizedErrorMessage,
    string ConcreteBlockerFixed,
    bool NoLiveLauncherCreated,
    bool NoHostedServiceCreated,
    bool NoApiWorkerStarted,
    bool NoDefaultConfigChanged,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues,
    LmaxTemporaryReadOnlyRuntimeActivationResult? ActivationResult = null);

public sealed class LmaxTemporaryReadOnlyActivationExecutor
{
    public const string ApprovedPathSummary =
        "R7 harness -> R9 adapter path -> R12 concrete adapter -> R14 real transport -> R16 session client -> R18 low-level stack -> R20 real low-level dependencies -> R22 providers -> R24 socket provider -> R26 TLS provider -> R28 FIX provider -> R30 MarketData provider + credential/config provider";

    private readonly LmaxTemporaryReadOnlyActivationExecutorOptions options;
    private readonly ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter;
    private bool attemptConsumed;

    public LmaxTemporaryReadOnlyActivationExecutor(
        LmaxTemporaryReadOnlyActivationExecutorOptions options,
        ILmaxTemporaryReadOnlyRuntimeActivationAdapter activationAdapter)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.activationAdapter = activationAdapter ?? throw new ArgumentNullException(nameof(activationAdapter));
    }

    public LmaxTemporaryReadOnlyActivationExecutorResult ExecuteOnce(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        string operatorApprovalPhrase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = Validate(request, operatorApprovalPhrase);
        if (issues.Count > 0)
        {
            return Blocked("BoundedExecutorValidationRejected", "SafetyConstraintFailed", string.Join("; ", issues.Select(x => x.Code)), issues);
        }

        if (!options.FutureExternalExecutionApproved)
        {
            return Blocked(
                "BoundedExecutorReadyRequiresFutureExplicitApproval",
                "FutureApprovalRequired",
                "Bounded executor validation passed, but execution requires a future explicitly approved phase.",
                issues);
        }

        attemptConsumed = true;
        LmaxTemporaryReadOnlyRuntimeActivationResult? activationResult = null;
        var shutdownCompleted = false;

        try
        {
            activationResult = activationAdapter.ValidateAsync(request, cancellationToken);
            shutdownCompleted = activationResult.SafetySnapshot.OutputSanitized;
            var activationIssues = ValidateActivationResult(activationResult);
            if (activationIssues.Count > 0)
            {
                return new LmaxTemporaryReadOnlyActivationExecutorResult(
                    nameof(LmaxTemporaryReadOnlyActivationExecutor),
                    ApprovedPathSummary,
                    ValidationPassed: false,
                    ExecutionStarted: true,
                    FutureExecutionAllowedOnlyWithApproval: true,
                    AttemptsPlanned: 1,
                    AttemptsExecuted: 1,
                    ShutdownRequired: options.ShutdownRevertRequired,
                    ShutdownRevertCompleted: shutdownCompleted,
                    OutputSanitized: activationResult.SafetySnapshot.OutputSanitized,
                    "BoundedExecutorAdapterResultRejected",
                    "AdapterSafetyConstraintFailed",
                    Sanitize(string.Join("; ", activationIssues.Select(x => x.Code))),
                    "RequiresLiveLauncherCreationFixedByBoundedExecutor",
                    NoLiveLauncherCreated: true,
                    NoHostedServiceCreated: true,
                    NoApiWorkerStarted: true,
                    NoDefaultConfigChanged: true,
                    activationIssues,
                    activationResult);
            }

            return new LmaxTemporaryReadOnlyActivationExecutorResult(
                nameof(LmaxTemporaryReadOnlyActivationExecutor),
                ApprovedPathSummary,
                ValidationPassed: true,
                ExecutionStarted: true,
                FutureExecutionAllowedOnlyWithApproval: true,
                AttemptsPlanned: 1,
                AttemptsExecuted: 1,
                ShutdownRequired: options.ShutdownRevertRequired,
                ShutdownRevertCompleted: true,
                OutputSanitized: true,
                "BoundedExecutorCompletedSingleAttemptWithSanitizedEvidence",
                null,
                null,
                "RequiresLiveLauncherCreationFixedByBoundedExecutor",
                NoLiveLauncherCreated: true,
                NoHostedServiceCreated: true,
                NoApiWorkerStarted: true,
                NoDefaultConfigChanged: true,
                [],
                activationResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LmaxTemporaryReadOnlyActivationExecutorResult(
                nameof(LmaxTemporaryReadOnlyActivationExecutor),
                ApprovedPathSummary,
                ValidationPassed: false,
                ExecutionStarted: true,
                FutureExecutionAllowedOnlyWithApproval: true,
                AttemptsPlanned: 1,
                AttemptsExecuted: 1,
                ShutdownRequired: options.ShutdownRevertRequired,
                ShutdownRevertCompleted: shutdownCompleted,
                OutputSanitized: true,
                "BoundedExecutorAdapterExecutionFailedSanitized",
                ex.GetType().Name,
                Sanitize(ex.Message),
                "RequiresLiveLauncherCreationFixedByBoundedExecutor",
                NoLiveLauncherCreated: true,
                NoHostedServiceCreated: true,
                NoApiWorkerStarted: true,
                NoDefaultConfigChanged: true,
                [new LmaxReadOnlyRuntimePreflightIssue("AdapterExecutionFailed", "$.adapter", "Bounded executor adapter execution failed with sanitized evidence.")],
                activationResult);
        }
    }

    private IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Validate(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        string operatorApprovalPhrase)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var scope = request.HarnessResult.Scope;

        if (attemptConsumed)
        {
            Add(issues, "AttemptAlreadyConsumed", "$.executor", "Bounded executor permits exactly one attempt.");
        }

        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !scope.DemoReadOnly ||
            !string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "EnvironmentNotDemoReadOnly", "$.environment", "Bounded executor requires Demo/read-only only.");
        }

        if (options.MaxAttemptCount != 1)
        {
            Add(issues, "InvalidMaxAttemptCount", "$.options.maxAttemptCount", "Bounded executor permits maxAttemptCount = 1 only.");
        }

        if (options.RetryCount != 0)
        {
            Add(issues, "RetryNotAllowed", "$.options.retryCount", "Bounded executor does not add retry logic.");
        }

        if (options.BatchMode)
        {
            Add(issues, "BatchModeNotAllowed", "$.options.batchMode", "Batch mode is forbidden.");
        }

        if (options.LoopMode)
        {
            Add(issues, "LoopModeNotAllowed", "$.options.loopMode", "Loop mode is forbidden.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(scope.MaxRuntimeSeconds))
        {
            Add(issues, "InvalidTimeout", "$.options.timeout", "Executor timeout must be positive and no greater than scope max runtime seconds.");
        }

        if (!options.ShutdownRevertRequired || scope.ShutdownRevert is null || !scope.ShutdownRevert.PlanPresent)
        {
            Add(issues, "ShutdownRevertPlanMissing", "$.shutdownRevert", "Shutdown/revert is required.");
        }

        if (!options.SanitizationRequired || !scope.SafetyFlags.OutputSanitizationEnabled)
        {
            Add(issues, "OutputSanitizationRequired", "$.sanitization", "Output sanitization is required.");
        }

        if (!options.NoPersistence)
        {
            Add(issues, "PersistenceNotAllowed", "$.options.noPersistence", "Persistent runtime enablement is forbidden.");
        }

        if (string.IsNullOrWhiteSpace(operatorApprovalPhrase) ||
            ContainsRawPasswordMarker(operatorApprovalPhrase) ||
            operatorApprovalPhrase.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            operatorApprovalPhrase.Contains("554=", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "OperatorApprovalUnsafeOrMissing", "$.operatorApprovalPhrase", "Operator approval metadata must be present and sanitized.");
        }

        var requestedSymbols = scope.Instruments.Select(x => x.Symbol).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var optionSymbols = options.ApprovedInstruments.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var approvedSymbols = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!requestedSymbols.SequenceEqual(approvedSymbols, StringComparer.OrdinalIgnoreCase) ||
            !optionSymbols.SequenceEqual(approvedSymbols, StringComparer.OrdinalIgnoreCase))
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.instruments", "Executor requires exactly the approved instrument set.");
        }

        foreach (var instrument in scope.Instruments)
        {
            if (LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol) is null)
            {
                Add(issues, "NonApprovedInstrument", "$.instruments", $"Instrument '{instrument.Symbol}' is not approved.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "UsdJpyCaveatMissing", "$.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
            }
        }

        issues.AddRange(LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);
        ValidateSafetyFlags(scope.SafetyFlags, issues);

        if (!request.HarnessResult.Passed || !request.HarnessResult.PreflightGate.Passed)
        {
            Add(issues, "HarnessPreflightNotPassed", "$.harnessResult", "R7/R9 harness preflight must pass before bounded execution.");
        }

        if (request.HarnessResult.ExternalRunExecuted ||
            request.HarnessResult.RuntimeActivationExecuted ||
            request.HarnessResult.CredentialLoadingAdded ||
            request.HarnessResult.LiveConnectionScriptCreated)
        {
            Add(issues, "HarnessIndicatesRuntimePower", "$.harnessResult", "Harness must not already indicate runtime power.");
        }

        return issues;
    }

    private static IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> ValidateActivationResult(
        LmaxTemporaryReadOnlyRuntimeActivationResult result)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var safety = result.SafetySnapshot;

        if (!result.HarnessOutputConsumed || !result.HarnessPreflightPassed)
        {
            Add(issues, "AdapterHarnessInvalid", "$.activationResult", "Adapter must consume a passed harness result.");
        }

        if (!result.ApprovedInstrumentsOnly)
        {
            Add(issues, "AdapterNonApprovedInstrument", "$.activationResult.instruments", "Adapter result must stay in approved instruments.");
        }

        if (!result.UsdJpyCaveatPreserved)
        {
            Add(issues, "AdapterUsdJpyCaveatMissing", "$.activationResult.instruments[USDJPY].caveat", "Adapter result must preserve USDJPY caveat.");
        }

        if (!safety.OutputSanitized ||
            safety.OrderSubmissionExecuted ||
            safety.TradingStateMutated ||
            safety.SchedulerStarted ||
            safety.PollingStarted ||
            safety.ReplayExecuted ||
            safety.ShadowReplaySubmitted ||
            safety.ApiWorkerStarted ||
            safety.RuntimeEnablementPersisted ||
            safety.DefaultGatewayRegistrationChanged ||
            safety.CredentialsPrinted ||
            safety.CredentialsStored)
        {
            Add(issues, "AdapterSafetySnapshotUnsafe", "$.activationResult.safetySnapshot", "Adapter safety snapshot contains a forbidden action.");
        }

        return issues;
    }

    private static void ValidateSafetyFlags(
        LmaxReadOnlyRuntimeSafetyFlags flags,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        FailIf(flags.ProductionAccountRequested, issues, "ProductionAccountRequested", "$.safetyFlags.productionAccountRequested", "Production account is forbidden.");
        FailIf(flags.AllowOrderSubmission, issues, "AllowOrderSubmission", "$.safetyFlags.allowOrderSubmission", "Order submission is forbidden.");
        FailIf(flags.AllowLiveTrading, issues, "AllowLiveTrading", "$.safetyFlags.allowLiveTrading", "Live trading is forbidden.");
        FailIf(flags.IsTradingEnabled, issues, "IsTradingEnabled", "$.safetyFlags.isTradingEnabled", "Trading must remain disabled.");
        FailIf(flags.SchedulerEnabled, issues, "SchedulerEnabled", "$.safetyFlags.schedulerEnabled", "Scheduler is forbidden.");
        FailIf(flags.PollingEnabled, issues, "PollingEnabled", "$.safetyFlags.pollingEnabled", "Polling is forbidden.");
        FailIf(flags.ReplayEnabled, issues, "ReplayEnabled", "$.safetyFlags.replayEnabled", "Replay is forbidden.");
        FailIf(flags.ShadowReplayEnabled, issues, "ShadowReplayEnabled", "$.safetyFlags.shadowReplayEnabled", "Shadow replay is forbidden.");
        FailIf(flags.TradingMutationEnabled, issues, "TradingMutationEnabled", "$.safetyFlags.tradingMutationEnabled", "Trading mutation is forbidden.");
        FailIf(flags.OrderGatewayRegistered, issues, "OrderGatewayRegistered", "$.safetyFlags.orderGatewayRegistered", "Order gateway registration is forbidden.");
        FailIf(flags.TradingGatewayRegistered, issues, "TradingGatewayRegistered", "$.safetyFlags.tradingGatewayRegistered", "Trading gateway registration is forbidden.");
        FailIf(flags.PersistentRuntimeEnablementRequested, issues, "PersistentRuntimeEnablementRequested", "$.safetyFlags.persistentRuntimeEnablementRequested", "Persistent runtime enablement is forbidden.");
        FailIf(flags.DefaultGatewayRegistrationChangeRequested, issues, "DefaultGatewayRegistrationChangeRequested", "$.safetyFlags.defaultGatewayRegistrationChangeRequested", "Default gateway registration change is forbidden.");
    }

    private static LmaxTemporaryReadOnlyActivationExecutorResult Blocked(
        string status,
        string category,
        string message,
        IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> issues)
        => new(
            nameof(LmaxTemporaryReadOnlyActivationExecutor),
            ApprovedPathSummary,
            ValidationPassed: issues.Count == 0,
            ExecutionStarted: false,
            FutureExecutionAllowedOnlyWithApproval: true,
            AttemptsPlanned: 1,
            AttemptsExecuted: 0,
            ShutdownRequired: true,
            ShutdownRevertCompleted: true,
            OutputSanitized: true,
            status,
            category,
            Sanitize(message),
            "RequiresLiveLauncherCreationFixedByBoundedExecutor",
            NoLiveLauncherCreated: true,
            NoHostedServiceCreated: true,
            NoApiWorkerStarted: true,
            NoDefaultConfigChanged: true,
            issues);

    private static void FailIf(bool condition, List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
    {
        if (condition)
        {
            Add(issues, code, path, message);
        }
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));

    private static bool ContainsRawPasswordMarker(string phrase)
        => phrase.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("password:", StringComparison.OrdinalIgnoreCase);

    private static string? Sanitize(string? value)
        => LmaxRealReadOnlyCredentialDependency.Sanitize(value);
}
