using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist(
    string ChecklistId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    string SourceEurgbpReadinessPath,
    string EurgbpReadinessDecision,
    string PreviousInstrument,
    string PreviousInstrumentClosureDecision,
    string PreviousDecision,
    string FutureCommandTemplate,
    bool ExternalRunAuthorized,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool IsApprovedForExternalRun,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool TradingMutation,
    bool BatchExecutionAllowed,
    bool OneInstrumentAtATime,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    IReadOnlyList<string> AbortCriteria,
    IReadOnlyList<string> RollbackSteps,
    IReadOnlyList<string> PostRunValidationSteps,
    LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision Decision);

public sealed record LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistCheck(
    string Name,
    LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistValidation(
    LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision FinalDecision,
    LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist Checklist,
    IReadOnlyList<LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistCheck> Checks);

public static class LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationOrRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\\s+(run|environment|authorization|execution)|uat\\s+(run|environment|authorization|execution)|environmentName\"?\\s*[:=]\\s*\"?(Production|UAT)|run\\s+is\\s+authorized|external\\s+run\\s+authorized|can\\s+run\\s+external|batch\\s+execution\\s+allowed|automatic\\s+retry|run\\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistValidation Validate(
        LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist checklist,
        LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration? readiness = null,
        string rawText = "")
    {
        var checks = new List<LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistCheck>
        {
            Check("OperatorAndReason", HasText(checklist.RequestedByOperatorId) && HasText(checklist.Reason), "Operator id and reason are required."),
            Check("EurgbpOnly", checklist.Symbol == "EURGBP" && checklist.SlashSymbol == "EUR/GBP", "Checklist must be for EURGBP / EUR/GBP only."),
            Check("SecurityId4003", checklist.PlanningSecurityId == "4003", "EURGBP SecurityID must be 4003."),
            Check("SecurityIdSource8", checklist.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("SnapshotProfile", checklist.RequestMode == "SnapshotPlusUpdates" && checklist.SymbolEncodingMode == "SecurityIdOnly" && checklist.MarketDepth == 1, "Snapshot profile must be SnapshotPlusUpdates / SecurityIdOnly / MarketDepth=1."),
            Check("ReadinessPath", HasText(checklist.SourceEurgbpReadinessPath), "Source EURGBP readiness artifact path is required."),
            Check("ReadinessPass", checklist.EurgbpReadinessDecision == "PASS", "EURGBP readiness decision must be PASS."),
            Check("PreviousDecision", checklist.PreviousInstrument == "GBPUSD" && checklist.PreviousInstrumentClosureDecision == "PASS" && checklist.PreviousDecision == "ProceedToEurgbpPlanning", "Previous state must be GBPUSD PASS and ProceedToEurgbpPlanning."),
            Check("FutureCommandMarkedNonExecutable", checklist.FutureCommandTemplate.Contains("DO NOT RUN IN PHASE 7F2", StringComparison.OrdinalIgnoreCase), "Future command template must be explicitly marked non-executable in Phase 7F2."),
            Check("FutureCommandEurgbp", checklist.FutureCommandTemplate.Contains("EURGBP", StringComparison.OrdinalIgnoreCase) && checklist.FutureCommandTemplate.Contains("4003", StringComparison.OrdinalIgnoreCase) && checklist.FutureCommandTemplate.Contains("SnapshotPlusUpdates", StringComparison.OrdinalIgnoreCase) && checklist.FutureCommandTemplate.Contains("SecurityIdOnly", StringComparison.OrdinalIgnoreCase), "Future template must identify EURGBP / 4003 / SnapshotPlusUpdates / SecurityIdOnly."),
            Check("ExternalRunAuthorizedFalse", !checklist.ExternalRunAuthorized, "externalRunAuthorized must remain false."),
            Check("CanRunExternalSnapshotFalse", !checklist.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !checklist.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("IsApprovedForExternalRunFalse", !checklist.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("NoSchedulerOrPolling", !checklist.SchedulerOrPolling, "Scheduler/polling must remain false."),
            Check("NoRuntimeShadowReplaySubmit", !checklist.RuntimeShadowReplaySubmit, "Runtime shadow replay submit must remain false."),
            Check("NoOrderSubmission", !checklist.OrderSubmission, "Order submission must remain false."),
            Check("NoTradingMutation", !checklist.TradingMutation, "Trading mutation must remain false."),
            Check("NoBatchExecution", !checklist.BatchExecutionAllowed, "Batch execution must remain false."),
            Check("OneInstrumentAtATime", checklist.OneInstrumentAtATime, "One-instrument-at-a-time must remain true."),
            Check("ApiWorkerFakeGateway", checklist.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentTrue", checklist.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("AbortCriteriaPresent", checklist.AbortCriteria.Count >= 8, "Abort criteria must be present."),
            Check("RollbackStepsPresent", checklist.RollbackSteps.Count >= 5, "Rollback steps must be present."),
            Check("PostRunValidationStepsPresent", checklist.PostRunValidationSteps.Count >= 5, "Post-run validation steps must be present."),
            Check("DecisionPass", checklist.Decision == LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS, "Complete safe checklist should be PASS.")
        };

        if (readiness is null)
        {
            checks.Add(Fail("ReadinessProvided", "EURGBP readiness rehydration artifact is required for validation."));
        }
        else
        {
            checks.Add(Check("ReadinessMatchesChecklist",
                readiness.FinalDecision == LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS
                && readiness.SelectedInstrument == checklist.Symbol
                && readiness.SlashSymbol == checklist.SlashSymbol
                && readiness.SecurityId == checklist.PlanningSecurityId
                && readiness.SecurityIdSource == checklist.SecurityIdSource
                && readiness.PreviousDecision == checklist.PreviousDecision
                && readiness.PreviousInstrumentClosureDecision == checklist.PreviousInstrumentClosureDecision
                && readiness.OneInstrumentAtATime
                && !readiness.BatchExecutionAllowed
                && !readiness.CanRunExternalSnapshot
                && !readiness.IsApprovedForExternalRun
                && !readiness.EligibleForManualSnapshotAttempt,
                "EURGBP readiness must match the checklist and remain non-executable."));
        }

        var combined = string.Join(" ",
            checklist.ChecklistId,
            checklist.RequestedByOperatorId,
            checklist.Reason,
            checklist.Symbol,
            checklist.SlashSymbol,
            checklist.PlanningSecurityId,
            checklist.SecurityIdSource,
            checklist.RequestMode,
            checklist.SymbolEncodingMode,
            checklist.SourceEurgbpReadinessPath,
            checklist.EurgbpReadinessDecision,
            checklist.PreviousInstrument,
            checklist.PreviousInstrumentClosureDecision,
            checklist.PreviousDecision,
            checklist.FutureCommandTemplate,
            checklist.ApiWorkerGatewayMode,
            string.Join(" ", checklist.AbortCriteria),
            string.Join(" ", checklist.RollbackSteps),
            string.Join(" ", checklist.PostRunValidationSteps),
            rawText);

        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveText", "Checklist contains credential-shaped or raw FIX content."));
        }

        if (AuthorizationOrRuntimePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoAuthorizationOrRuntimeText", "Checklist must not imply current authorization, automation, order, scheduler, production/UAT, runtime replay submit, or batch execution."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL)
            ? LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL
            : LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS;

        return new(final, checklist with { Decision = final }, checks);
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.FAIL, detail);
}
