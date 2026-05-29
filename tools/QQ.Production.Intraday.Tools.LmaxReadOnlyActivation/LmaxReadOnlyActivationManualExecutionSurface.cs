using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public sealed record LmaxReadOnlyActivationManualExecutionSurfaceCommand(
    string Phase,
    string ExpectedOperatorApprovalPhrase,
    string OperatorApprovalPhrase,
    bool ExecuteOnceRequested,
    bool ManualOperatorConfirmation,
    bool SingleAttemptOnly,
    bool NoApiWorkerStartup,
    bool NoServiceSchedulerPolling,
    bool NoOrderTradingPath,
    bool NoCredentialOutput);

public sealed record LmaxReadOnlyActivationManualExecutionSurfaceValidationResult(
    bool Passed,
    string Status,
    bool ExecuteOnceNotInvokedByApprovedOperationalCallerInR59ResolvedForNextRetry,
    bool ApprovedManualExecutionSurfaceProvable,
    bool CallsManualOperationalCaller,
    bool CallOnceCallsInvokeOnce,
    bool InvokeOnceCallsExecuteOnce,
    bool ExactPerPhaseOperatorApprovalRequired,
    bool ExactPerPhaseOperatorApprovalPresent,
    bool RetryPhaseReserved,
    bool SingleAttemptOnly,
    bool ManualOnly,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool UnreachableFromApiWorkerDefaultStartup,
    bool NoLauncherServiceSchedulerPolling,
    bool CredentialValuesReturned,
    bool ExternalBoundaryAttempted,
    LmaxTemporaryReadOnlySessionBoundaryStatus CredentialConfigBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataRequestBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataResponseBoundary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed record LmaxReadOnlyActivationManualExecutionSurfaceResult(
    LmaxReadOnlyActivationManualExecutionSurfaceValidationResult Validation,
    LmaxManualBoundedReadOnlyActivationCallerExecutionResult? CallerResult)
{
    public bool CallOnceInvoked => CallerResult is not null;

    public bool InvokeOnceInvoked => CallerResult?.InvocationResult is not null;

    public bool ExecuteOnceInvoked => CallerResult?.InvocationResult?.ExecutorResult is not null;

    public int AttemptCount => CallerResult?.InvocationResult?.ExecutorResult?.AttemptsExecuted ?? 0;
}

public sealed class LmaxReadOnlyActivationManualExecutionSurface
{
    private readonly Func<LmaxReadOnlyActivationManualExecutionSurfaceCommand, LmaxManualBoundedReadOnlyActivationCallerRequest> requestFactory;
    private readonly Func<LmaxManualBoundedReadOnlyActivationCaller> callerFactory;
    private bool executionConsumed;

    public LmaxReadOnlyActivationManualExecutionSurface(
        Func<LmaxReadOnlyActivationManualExecutionSurfaceCommand, LmaxManualBoundedReadOnlyActivationCallerRequest> requestFactory,
        Func<LmaxManualBoundedReadOnlyActivationCaller> callerFactory)
    {
        this.requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        this.callerFactory = callerFactory ?? throw new ArgumentNullException(nameof(callerFactory));
    }

    public LmaxReadOnlyActivationManualExecutionSurfaceValidationResult Validate(
        LmaxReadOnlyActivationManualExecutionSurfaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        ValidateCommand(command, issues);

        LmaxManualBoundedReadOnlyActivationCallerValidationResult? callerValidation = null;
        LmaxManualBoundedReadOnlyActivationCallerRequest? callerRequest = null;
        if (issues.Count == 0)
        {
            callerRequest = requestFactory(command);
            callerValidation = callerFactory().Validate(callerRequest);
            if (!callerValidation.Passed)
            {
                Add(issues, "OperationalCallerValidationFailed", "$.operationalCaller", "The approved manual execution surface requires the R58 operational caller to validate.");
                issues.AddRange(callerValidation.Issues);
            }
        }

        var invocationValidation = callerRequest?.InvocationRequest is null
            ? null
            : new LmaxBoundedReadOnlyActivationInvocationPath(new LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter()).Validate(callerRequest.InvocationRequest);
        var scope = callerRequest?.InvocationRequest.ActivationRequest.HarnessResult.Scope;
        var approvedInstrumentsExact = scope is not null && ApprovedInstrumentsExact(scope);
        var usdJpyCaveatPreserved = scope is not null && UsdJpyCaveatPreserved(scope);
        var passed = issues.Count == 0;

        return new LmaxReadOnlyActivationManualExecutionSurfaceValidationResult(
            Passed: passed,
            Status: passed
                ? "ApprovedManualActivationExecutionSurfaceReadyNoExternalActivation"
                : "ApprovedManualActivationExecutionSurfaceRejected",
            ExecuteOnceNotInvokedByApprovedOperationalCallerInR59ResolvedForNextRetry: passed,
            ApprovedManualExecutionSurfaceProvable: passed,
            CallsManualOperationalCaller: true,
            CallOnceCallsInvokeOnce: callerValidation?.CallsBoundedInvocationPath == true,
            InvokeOnceCallsExecuteOnce: callerValidation?.InvocationPathCallsExecuteOnce == true,
            ExactPerPhaseOperatorApprovalRequired: true,
            ExactPerPhaseOperatorApprovalPresent: callerValidation?.ExactPerPhaseOperatorApprovalPresent == true,
            RetryPhaseReserved: callerValidation?.RetryPhaseReserved == true && LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(command.Phase),
            SingleAttemptOnly: command.SingleAttemptOnly && callerValidation?.SingleAttemptOnly == true,
            ManualOnly: command.ManualOperatorConfirmation && callerValidation?.ManualOnly == true,
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            UnreachableFromApiWorkerDefaultStartup: command.NoApiWorkerStartup && callerValidation?.ApiWorkerStartupRequired == false,
            NoLauncherServiceSchedulerPolling: command.NoServiceSchedulerPolling &&
                                               callerValidation?.LiveLauncherRequired == false &&
                                               callerValidation?.HostedBackgroundServiceRequired == false &&
                                               callerValidation?.SchedulerPollingRequired == false,
            CredentialValuesReturned: callerValidation?.CredentialValuesReturned == true || invocationValidation?.CredentialValuesReturned == true,
            ExternalBoundaryAttempted: callerValidation?.ExternalBoundaryAttempted == true || invocationValidation?.ExternalBoundaryAttempted == true,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TcpBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TlsBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            FixLogonBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataRequestBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataResponseBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            Issues: issues);
    }

    public LmaxReadOnlyActivationManualExecutionSurfaceResult ExecuteOnce(
        LmaxReadOnlyActivationManualExecutionSurfaceCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = Validate(command);
        if (!validation.Passed || !command.ExecuteOnceRequested)
        {
            return new LmaxReadOnlyActivationManualExecutionSurfaceResult(validation, null);
        }

        if (executionConsumed)
        {
            var rejected = validation with
            {
                Passed = false,
                Status = "ApprovedManualActivationExecutionSurfaceRejected",
                ExecuteOnceNotInvokedByApprovedOperationalCallerInR59ResolvedForNextRetry = false,
                ApprovedManualExecutionSurfaceProvable = false,
                Issues =
                [
                    new LmaxReadOnlyRuntimePreflightIssue(
                        "ManualExecutionSurfaceAlreadyConsumed",
                        "$.manualExecutionSurface",
                        "The manual activation execution surface permits exactly one call per surface instance.")
                ]
            };
            return new LmaxReadOnlyActivationManualExecutionSurfaceResult(rejected, null);
        }

        executionConsumed = true;
        var request = requestFactory(command);
        var callerResult = callerFactory().CallOnce(request, cancellationToken);
        return new LmaxReadOnlyActivationManualExecutionSurfaceResult(validation, callerResult);
    }

    private static void ValidateCommand(
        LmaxReadOnlyActivationManualExecutionSurfaceCommand command,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(command.Phase))
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.phase", "Manual activation execution is limited to approved odd-numbered LMAX retry phases.");
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedOperatorApprovalPhrase) ||
            !string.Equals(command.ExpectedOperatorApprovalPhrase, command.OperatorApprovalPhrase, StringComparison.Ordinal) ||
            !command.OperatorApprovalPhrase.Contains($"Phase {command.Phase}", StringComparison.Ordinal) ||
            UnsafeApprovalPhrase(command.OperatorApprovalPhrase))
        {
            Add(issues, "ExactPerPhaseOperatorApprovalMissing", "$.operatorApproval", "The manual execution surface requires exact operator approval for the current phase.");
        }

        if (!command.ExecuteOnceRequested)
        {
            Add(issues, "ManualExecuteOnceFlagMissing", "$.executeOnceRequested", "The manual execution surface requires an explicit execute-once command input.");
        }

        if (!command.ManualOperatorConfirmation)
        {
            Add(issues, "ManualOperatorConfirmationMissing", "$.manualOperatorConfirmation", "The manual execution surface requires explicit manual operator confirmation.");
        }

        if (!command.SingleAttemptOnly)
        {
            Add(issues, "SingleAttemptProofMissing", "$.singleAttemptOnly", "The manual execution surface permits one attempt only.");
        }

        if (!command.NoApiWorkerStartup)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartup", "The manual execution surface must remain unreachable from API/Worker startup.");
        }

        if (!command.NoServiceSchedulerPolling)
        {
            Add(issues, "ServiceSchedulerPollingRisk", "$.noServiceSchedulerPolling", "The manual execution surface must not be a service, scheduler, or polling loop.");
        }

        if (!command.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "The manual execution surface must not expose orders or trading mutation.");
        }

        if (!command.NoCredentialOutput)
        {
            Add(issues, "CredentialOutputRisk", "$.noCredentialOutput", "The manual execution surface must not expose credential values.");
        }
    }

    private static bool ApprovedInstrumentsExact(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var expected = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actual = scope.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return actual.Length == expected.Length &&
               actual.Zip(expected).All(pair =>
                   string.Equals(pair.First.Symbol, pair.Second.Symbol, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(pair.First.SecurityId, pair.Second.SecurityId, StringComparison.Ordinal) &&
                   string.Equals(pair.First.SecurityIdSource, pair.Second.SecurityIdSource, StringComparison.Ordinal));
    }

    private static bool UsdJpyCaveatPreserved(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));

    private static bool UnsafeApprovalPhrase(string phrase)
        => ContainsRawPasswordMarker(phrase) ||
           phrase.Contains("token", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("554=", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsRawPasswordMarker(string phrase)
        => phrase.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
           phrase.Contains("password:", StringComparison.OrdinalIgnoreCase);

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}

public sealed class LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter : ILmaxTemporaryReadOnlyRuntimeActivationAdapter
{
    public int Calls { get; private set; }

    public LmaxTemporaryReadOnlyRuntimeActivationResult ValidateAsync(
        LmaxTemporaryReadOnlyRuntimeActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;

        return new LmaxTemporaryReadOnlyRuntimeActivationResult(
            request.Phase,
            request.CreatedAtUtc,
            request.AdapterMode,
            LmaxTemporaryReadOnlyRuntimeActivationOutcome.BoundedExecutableReadOnlyAccepted,
            HarnessOutputConsumed: true,
            HarnessPreflightPassed: true,
            ApprovedInstrumentsOnly: true,
            UsdJpyCaveatPreserved: true,
            DryRunOnly: false,
            FutureR10ApprovalRequired: false,
            "Manual execution surface adapter accepted through approved call chain without external boundary use.",
            [],
            request.HarnessResult.Scope.Instruments.Select(x => new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
                x.Symbol,
                x.SecurityId,
                x.SecurityIdSource,
                "Demo/read-only",
                LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted,
                "NotAttempted",
                null,
                request.CreatedAtUtc,
                x.Caveat)).ToList(),
            LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork);
    }
}
