using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome
{
    PendingGbpusdMarketHoursAttempt,
    ProceedToEurgbpPlanning,
    RetryGbpusdAtLaterMarketHours,
    BlockSequenceForDiagnostics,
    StopManualWorkflow
}

public enum LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyPostGbpusdNextInstrumentDecision(
    string DecisionId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string SourceWorkflowPlanPath,
    string? SourceGbpusdClosureManifestPath,
    string? SourceGbpusdReviewPath,
    string CurrentInstrument,
    string? NextCandidateInstrument,
    int SequenceOrder,
    string? GbpusdClosureStatus,
    string? GbpusdClosureDecision,
    string? GbpusdClosureClassification,
    LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome Decision,
    string RequiredNextPhase,
    bool CanRunExternalSnapshot,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool BatchExecutionAllowed,
    int ExecutableCount,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyPostGbpusdNextInstrumentDecisionCheck(
    string Name,
    LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidation(
    LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyPostGbpusdNextInstrumentDecisionCheck> Checks);

public static class LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForbiddenRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest|TradeCaptureReportRequest|SubmitOrder|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome Decide(
        string? gbpusdClosureClassification,
        string? gbpusdClosureDecision)
    {
        if (string.IsNullOrWhiteSpace(gbpusdClosureClassification) && string.IsNullOrWhiteSpace(gbpusdClosureDecision))
        {
            return LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.PendingGbpusdMarketHoursAttempt;
        }

        if (gbpusdClosureClassification == "CompletedWithBook" && gbpusdClosureDecision == "PASS")
        {
            return LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning;
        }

        if (gbpusdClosureClassification == "CompletedWithEmptyBook" && gbpusdClosureDecision == "PASS_WITH_KNOWN_WARNINGS")
        {
            return LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.RetryGbpusdAtLaterMarketHours;
        }

        return LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.BlockSequenceForDiagnostics;
    }

    public static string RequiredNextPhaseFor(LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome decision)
        => decision switch
        {
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.PendingGbpusdMarketHoursAttempt => "Wait for market hours, run the separate operator-approved GBPUSD manual snapshot command, then complete Phase 7C closure.",
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning => "Phase 7E - EURGBP Manual Snapshot Readiness Refresh / No External Run.",
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.RetryGbpusdAtLaterMarketHours => "Phase 7E - Controlled GBPUSD Market-Hours Retry Planning / No External Run.",
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.BlockSequenceForDiagnostics => "Phase 7E - GBPUSD Closure Diagnostics / No External Run.",
            LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.StopManualWorkflow => "Stop manual additional-instrument workflow.",
            _ => "Manual operator review required."
        };

    public static LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidation Validate(
        LmaxReadOnlyPostGbpusdNextInstrumentDecision decision,
        string rawDecisionText = "")
    {
        var checks = new List<LmaxReadOnlyPostGbpusdNextInstrumentDecisionCheck>
        {
            Check("OperatorAndReason", HasText(decision.RequestedByOperatorId) && HasText(decision.Reason), "Operator id and reason are required."),
            Check("WorkflowPlanReference", HasText(decision.SourceWorkflowPlanPath), "Workflow plan reference is required."),
            Check("CurrentInstrumentGbpusd", decision.CurrentInstrument == "GBPUSD" && decision.SequenceOrder == 1, "Current instrument must be GBPUSD at sequence order 1."),
            Check("NextCandidateOnlyWhenProceeding", decision.Decision == LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning ? decision.NextCandidateInstrument == "EURGBP" : string.IsNullOrWhiteSpace(decision.NextCandidateInstrument), "EURGBP may only be next candidate after CompletedWithBook PASS."),
            Check("RequiredNextPhase", HasText(decision.RequiredNextPhase), "Required next phase is required."),
            Check("RunEligibilityFalse", !decision.CanRunExternalSnapshot && !decision.IsApprovedForExternalRun && !decision.EligibleForManualSnapshotAttempt, "Run eligibility flags must remain false."),
            Check("NoBatchExecution", !decision.BatchExecutionAllowed, "Batch execution must remain disabled."),
            Check("ExecutableCountZero", decision.ExecutableCount == 0, "No instrument may be executable."),
            Check("AggregateSafetyFlags", !decision.SchedulerOrPolling && !decision.RuntimeShadowReplaySubmit && !decision.OrderSubmission && !decision.GatewayRegistration && !decision.TradingMutation, "Scheduler, replay, order, gateway, and mutation flags must remain false."),
            Check("FakeGatewayOnly", decision.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentFlag", decision.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        var expectedDecision = Decide(decision.GbpusdClosureClassification, decision.GbpusdClosureDecision);
        checks.Add(Check("DecisionMatchesClosure", decision.Decision == expectedDecision, $"Decision must match closure state. Expected {expectedDecision}."));

        if (decision.Decision == LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning)
        {
            checks.Add(Check("ProceedRequiresBookPass", decision.GbpusdClosureClassification == "CompletedWithBook" && decision.GbpusdClosureDecision == "PASS", "Proceeding to EURGBP requires GBPUSD CompletedWithBook PASS."));
        }

        if (decision.Decision == LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.RetryGbpusdAtLaterMarketHours)
        {
            checks.Add(Check("RetryRequiresEmptyBookWarning", decision.GbpusdClosureClassification == "CompletedWithEmptyBook" && decision.GbpusdClosureDecision == "PASS_WITH_KNOWN_WARNINGS", "Retry requires a safe GBPUSD empty-book warning."));
        }

        if (SensitivePattern.IsMatch(rawDecisionText))
        {
            checks.Add(Fail("NoSensitiveText", "Decision artifact contains credential-shaped or raw FIX content."));
        }

        if (ForbiddenRuntimePattern.IsMatch(rawDecisionText))
        {
            checks.Add(Fail("NoRuntimeSurfaceText", "Decision artifact contains forbidden runtime/order/scheduler marker."));
        }

        var hasFail = checks.Any(x => x.Decision == LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL);
        var hasWarningOutcome = decision.Decision is LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.PendingGbpusdMarketHoursAttempt
            or LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.RetryGbpusdAtLaterMarketHours
            or LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.BlockSequenceForDiagnostics;
        var final = hasFail
            ? LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL
            : hasWarningOutcome
                ? LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS;
        return new(final, checks);
    }

    private static bool HasText(string value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyPostGbpusdNextInstrumentDecisionCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyPostGbpusdNextInstrumentDecisionCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyPostGbpusdNextInstrumentDecisionValidationDecision.FAIL, detail);
}
