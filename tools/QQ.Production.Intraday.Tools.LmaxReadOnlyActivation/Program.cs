using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return args.Length == 0 ? 2 : 0;
}

var options = Parse(args);
if (!options.TryGetValue("--phase", out var phase) ||
    !options.TryGetValue("--approval-file", out var approvalFile) ||
    !options.TryGetValue("--expected-approval-file", out var expectedApprovalFile))
{
    PrintUsage();
    return 2;
}

var approval = File.ReadAllText(approvalFile).Trim();
var expectedApproval = File.ReadAllText(expectedApprovalFile).Trim();
var command = new LmaxReadOnlyActivationManualExecutionSurfaceCommand(
    phase,
    expectedApproval,
    approval,
    ExecuteOnceRequested: options.ContainsKey("--execute-once"),
    ManualOperatorConfirmation: options.ContainsKey("--manual-confirm"),
    SingleAttemptOnly: options.ContainsKey("--single-attempt-only"),
    NoApiWorkerStartup: options.ContainsKey("--no-api-worker-startup"),
    NoServiceSchedulerPolling: options.ContainsKey("--no-service-scheduler-polling"),
    NoOrderTradingPath: options.ContainsKey("--no-order-trading-path"),
    NoCredentialOutput: options.ContainsKey("--no-credential-output"));

var adapterMode = options.TryGetValue("--adapter-mode", out var requestedAdapterMode)
    ? requestedAdapterMode
    : LmaxReadOnlyActivationManualExecutionSurfaceFactory.NoExternalBoundaryMode;
if (!LmaxReadOnlyActivationManualExecutionSurfaceFactory.IsApprovedAdapterMode(adapterMode))
{
    Console.WriteLine("status=RejectedUnapprovedAdapterMode");
    return 2;
}

var surface = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateForManualTool(adapterMode);
var result = surface.ExecuteOnce(command);
var executorResult = result.CallerResult?.InvocationResult?.ExecutorResult;
var activationResult = executorResult?.ActivationResult;
var safety = activationResult?.SafetySnapshot;
var tlsEvidence = activationResult?.TlsEvidence ?? LmaxSanitizedTlsBoundaryEvidence.NotAttempted;

Console.WriteLine($"phase={phase}");
Console.WriteLine($"adapterMode={adapterMode}");
Console.WriteLine($"status={result.Validation.Status}");
Console.WriteLine($"passed={result.Validation.Passed}");
Console.WriteLine($"callOnceInvoked={result.CallOnceInvoked}");
Console.WriteLine($"invokeOnceInvoked={result.InvokeOnceInvoked}");
Console.WriteLine($"executeOnceInvoked={result.ExecuteOnceInvoked}");
Console.WriteLine($"attemptCount={result.AttemptCount}");
Console.WriteLine($"executorExecutionStarted={executorResult?.ExecutionStarted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"executorValidationPassed={executorResult?.ValidationPassed.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"executorStatus={executorResult?.SanitizedStatus ?? "NotInvoked"}");
Console.WriteLine($"executorErrorCategory={executorResult?.SanitizedErrorCategory ?? "None"}");
Console.WriteLine($"activationOutcome={activationResult?.Outcome.ToString() ?? "NotInvoked"}");
Console.WriteLine($"activationIssueCodes={string.Join(",", activationResult?.Issues.Select(x => x.Code) ?? [])}");
Console.WriteLine($"externalActivationAttempted={safety?.ExternalRunExecuted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"tcpConnectionAttempted={safety?.TcpConnectionAttempted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"tlsHandshakeAttempted={safety?.TlsHandshakeAttempted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"tlsSucceeded={tlsEvidence.TlsSucceeded.ToString().ToLowerInvariant()}");
Console.WriteLine($"tlsBoundaryStatus={tlsEvidence.TlsBoundaryStatus}");
Console.WriteLine($"tlsResultCategory={tlsEvidence.TlsResultCategory}");
Console.WriteLine($"tlsFailureCategory={tlsEvidence.TlsFailureCategory ?? "None"}");
Console.WriteLine($"tlsTimedOut={tlsEvidence.TlsTimedOut.ToString().ToLowerInvariant()}");
Console.WriteLine($"tlsExceptionCategory={tlsEvidence.TlsExceptionCategory ?? "None"}");
Console.WriteLine($"tlsStreamAvailableForFix={tlsEvidence.TlsStreamAvailableForFix.ToString().ToLowerInvariant()}");
Console.WriteLine($"tlsRawMaterialSerialized={tlsEvidence.TlsRawMaterialSerialized.ToString().ToLowerInvariant()}");
Console.WriteLine($"fixLogonAttempted={safety?.FixLogonAttempted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"fixBoundaryStatus={activationResult?.FixLogonBoundary.ToString() ?? "NotAttempted"}");
Console.WriteLine($"fixBoundarySanitizedStatus={activationResult?.FixBoundarySanitizedStatus ?? "NotAttempted"}");
Console.WriteLine($"fixBoundaryResultCategory={activationResult?.FixBoundarySanitizedErrorCategory ?? (activationResult?.FixLogonBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded ? "Succeeded" : "None")}");
Console.WriteLine($"fixAcknowledgementCategory={FixAcknowledgementCategory(activationResult)}");
Console.WriteLine($"marketDataRequestSent={safety?.MarketDataRequestSent.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataRequestSentLegacyFlag={safety?.MarketDataRequestSentLegacyFlag.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataRequestWriteAttempted={safety?.MarketDataRequestWriteAttempted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataRequestWriteSucceeded={safety?.MarketDataRequestWriteSucceeded.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataRequestResponseReadAttempted={safety?.MarketDataRequestResponseReadAttempted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataRequestReachedBoundedResponseClassification={safety?.MarketDataRequestReachedBoundedResponseClassification.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"marketDataBoundaryStatus={activationResult?.MarketDataBoundary.ToString() ?? "NotAttempted"}");
Console.WriteLine($"marketDataBoundarySanitizedStatus={activationResult?.MarketDataBoundarySanitizedStatus ?? "NotAttempted"}");
Console.WriteLine($"marketDataBoundaryResultCategory={activationResult?.MarketDataBoundarySanitizedErrorCategory ?? (activationResult?.MarketDataBoundary is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded ? "Succeeded" : "None")}");
Console.WriteLine($"sessionRejectSanitizedReasonCategory={SanitizedSessionRejectReasonCategory(activationResult)}");
Console.WriteLine($"sanitizedSessionRejectReasonCategory={SanitizedSessionRejectReasonCategory(activationResult)}");
Console.WriteLine($"marketDataRejectSanitizedSubcategory={activationResult?.MarketDataRejectSanitizedSubcategory ?? "RejectReasonNotAvailable"}");
Console.WriteLine($"sessionRejectSanitizedSubcategory={activationResult?.SessionRejectSanitizedSubcategory ?? "RejectReasonNotAvailable"}");
Console.WriteLine($"rejectReasonExtractionSource={activationResult?.RejectReasonExtractionSource ?? "RejectReasonNotAvailable"}");
Console.WriteLine($"sessionRejectRefTagIdSanitizedCategory={activationResult?.SessionRejectRefTagIdSanitizedCategory ?? "RefTagID_NotAvailable"}");
Console.WriteLine($"sessionRejectReasonSanitizedCategory={activationResult?.SessionRejectReasonSanitizedCategory ?? "SessionRejectReason_NotAvailable"}");
Console.WriteLine($"sessionRejectRefMsgTypeSanitizedCategory={activationResult?.SessionRejectRefMsgTypeSanitizedCategory ?? "RefMsgType_NotAvailable"}");
Console.WriteLine($"marketDataEntriesObserved={NullableBool(activationResult?.MarketDataEntriesObserved)}");
Console.WriteLine($"marketDataSanitizedEntryCount={NullableInt(activationResult?.MarketDataSanitizedEntryCount)}");
Console.WriteLine($"marketDataEntriesEvidenceCategory={activationResult?.MarketDataEntriesEvidenceCategory ?? "EntriesEvidenceInconclusiveSafe"}");
Console.WriteLine($"marketDataEntriesReportingSource={activationResult?.MarketDataEntriesReportingSource ?? "EntriesNotEmittedByCli"}");
Console.WriteLine($"marketDataEntriesNotAvailableReason={activationResult?.MarketDataEntriesNotAvailableReason ?? "None"}");
Console.WriteLine($"logoutObserved={activationResult?.LogoutObserved.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"logoutSourceCategory={activationResult?.LogoutSourceCategory ?? "LogoutEvidenceInconclusiveSafe"}");
Console.WriteLine($"logoutReasonSanitizedCategory={activationResult?.LogoutReasonSanitizedCategory ?? "LogoutReasonNotAvailable"}");
Console.WriteLine($"logoutTextPresentSanitized={NullableBool(activationResult?.LogoutTextPresentSanitized)}");
Console.WriteLine($"logoutAfterInstrument={activationResult?.LogoutAfterInstrument ?? "None"}");
Console.WriteLine($"logoutAfterSecurityIdSanitized={activationResult?.LogoutAfterSecurityIdSanitized ?? "None"}");
Console.WriteLine($"logoutTimingCategory={activationResult?.LogoutTimingCategory ?? "LogoutEvidenceInconclusiveSafe"}");
Console.WriteLine($"logoutReasonExtractionSource={activationResult?.LogoutReasonExtractionSource ?? "LogoutReasonNotAvailable"}");
foreach (var status in activationResult?.InstrumentStatuses ?? [])
{
    var key = status.Symbol.ToLowerInvariant();
    Console.WriteLine($"instrument.{key}.selectedInstrument={status.Symbol}");
    Console.WriteLine($"instrument.{key}.securityId={status.SecurityId}");
    Console.WriteLine($"instrument.{key}.securityIdSource={status.SecurityIdSource}");
    Console.WriteLine($"instrument.{key}.marketDataBoundary={status.BoundaryStatus}");
    Console.WriteLine($"instrument.{key}.marketDataResponseCategory={status.SanitizedErrorCategory ?? "Succeeded"}");
    Console.WriteLine($"instrument.{key}.marketDataEntriesObserved={(status.MarketDataSnapshotCount > 0).ToString().ToLowerInvariant()}");
    Console.WriteLine($"instrument.{key}.marketDataSanitizedEntryCount={status.MarketDataSnapshotCount}");
    Console.WriteLine($"instrument.{key}.marketDataEntriesEvidenceCategory={(status.MarketDataSnapshotCount > 0 ? "EntriesObservedWithSanitizedCount" : "NoEntriesObserved")}");
    Console.WriteLine($"instrument.{key}.marketDataEntriesReportingSource=InstrumentStatusSanitizedSnapshotCount");
    Console.WriteLine($"instrument.{key}.marketDataEntriesNotAvailableReason=None");
    Console.WriteLine($"instrument.{key}.sessionRejectCount={status.SessionRejectCount}");
    Console.WriteLine($"instrument.{key}.marketDataRequestRejectCount={status.MarketDataRequestRejectCount}");
    Console.WriteLine($"instrument.{key}.businessMessageRejectCount={status.BusinessMessageRejectCount}");
    Console.WriteLine($"instrument.{key}.sanitizedReasonCategory={status.SanitizedReasonCategory ?? "None"}");
}
Console.WriteLine($"transportSanitizedStatus={activationResult?.TransportSanitizedStatus ?? "NotInvoked"}");
Console.WriteLine($"transportResultCategory={activationResult?.TransportSanitizedErrorCategory ?? "None"}");
Console.WriteLine($"realSocketOpened={safety?.RealSocketOpened.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine($"shutdownRevertCompleted={executorResult?.ShutdownRevertCompleted.ToString().ToLowerInvariant() ?? "false"}");
Console.WriteLine("credentialValuesReturned=false");
Console.WriteLine("outputSanitized=true");

return result.Validation.Passed ? 0 : 1;

static Dictionary<string, string> Parse(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var current = args[i];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            options[current] = args[++i];
        }
        else
        {
            options[current] = "true";
        }
    }

    return options;
}

static void PrintUsage()
{
    Console.WriteLine("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation");
    Console.WriteLine("Manual one-shot LMAX Demo read-only activation surface.");
    Console.WriteLine("Required: --phase LMAX-R<number> --approval-file <path> --expected-approval-file <path> --execute-once --manual-confirm --single-attempt-only --no-api-worker-startup --no-service-scheduler-polling --no-order-trading-path --no-credential-output --adapter-mode no-external-boundary|real-bounded-executable-readonly");
}

static string FixAcknowledgementCategory(LmaxTemporaryReadOnlyRuntimeActivationResult? activationResult)
{
    if (activationResult is null ||
        activationResult.FixLogonBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted)
    {
        return "FixAcknowledgementNotAttempted";
    }

    if (activationResult.FixLogonBoundary is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded)
    {
        return "FixLogonAcknowledged";
    }

    return activationResult.FixBoundarySanitizedErrorCategory ?? "FixReadUnknownFailure";
}

static string SanitizedSessionRejectReasonCategory(LmaxTemporaryReadOnlyRuntimeActivationResult? activationResult)
{
    if (activationResult?.MarketDataBoundarySanitizedErrorCategory is null ||
        !activationResult.MarketDataBoundarySanitizedErrorCategory.StartsWith("SessionRejectObserved", StringComparison.Ordinal))
    {
        return "None";
    }

    return activationResult.MarketDataBoundarySanitizedErrorMessage ?? "SessionRejectReasonNotAvailable";
}

static string NullableBool(bool? value)
    => value.HasValue ? value.Value.ToString().ToLowerInvariant() : "notavailable";

static string NullableInt(int? value)
    => value.HasValue ? value.Value.ToString() : "notavailable";
