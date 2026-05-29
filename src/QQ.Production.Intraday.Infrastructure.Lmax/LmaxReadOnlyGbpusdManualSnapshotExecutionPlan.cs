using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdManualSnapshotExecutionPlan(
    string PlanId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    string SourceAttemptGatePath,
    string AttemptGateDecision,
    string FutureCommandTemplate,
    bool ExternalRunAuthorized,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool IsApprovedForExternalRun,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    IReadOnlyList<string> AbortCriteria,
    IReadOnlyList<string> RollbackSteps,
    IReadOnlyList<string> PostRunValidationSteps,
    LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision Decision);

public sealed record LmaxReadOnlyGbpusdManualSnapshotExecutionPlanCheck(
    string Name,
    LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdManualSnapshotExecutionPlanResult(
    LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision FinalDecision,
    LmaxReadOnlyGbpusdManualSnapshotExecutionPlan Plan,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotExecutionPlanCheck> Checks);

public static class LmaxReadOnlyGbpusdManualSnapshotExecutionPlanValidator
{
    private static readonly Regex SensitivePattern = new("(password|secret|token|apikey|api_key|privatekey|private_key|bearer|\\b553=|\\b554=|host=|user=|account)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdManualSnapshotExecutionPlanResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan plan,
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate? attemptGate)
    {
        var checks = new List<LmaxReadOnlyGbpusdManualSnapshotExecutionPlanCheck>
        {
            Check("GbpusdOnly", plan.Symbol == "GBPUSD" && plan.SlashSymbol == "GBP/USD", "Plan must be for GBPUSD / GBP/USD only."),
            Check("SecurityId4002", plan.PlanningSecurityId == "4002", "GBPUSD SecurityID must be 4002."),
            Check("SecurityIdSource8", plan.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", plan.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", plan.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", plan.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", plan.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", plan.MarketDepth == 1, "MarketDepth must be 1."),
            Check("AttemptGateExists", attemptGate is not null, "Phase 6S attempt gate is required."),
            Check("AttemptGatePass", attemptGate?.GateDecision == LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS && plan.AttemptGateDecision == "PASS", "Attempt gate must be PASS."),
            Check("AttemptGateMatchesPlan", attemptGate is not null && attemptGate.Symbol == plan.Symbol && attemptGate.SlashSymbol == plan.SlashSymbol && attemptGate.PlanningSecurityId == plan.PlanningSecurityId && attemptGate.SecurityIdSource == plan.SecurityIdSource, "Attempt gate must match symbol and SecurityID."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(plan.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(plan.Reason), "Reason is required."),
            Check("FutureCommandMarkedNonExecutable", plan.FutureCommandTemplate.Contains("DO NOT RUN IN PHASE 6T", StringComparison.OrdinalIgnoreCase), "Future command template must be explicitly marked non-executable in Phase 6T."),
            Check("ExternalRunAuthorizedFalse", !plan.ExternalRunAuthorized, "externalRunAuthorized must remain false."),
            Check("CanRunExternalSnapshotFalse", !plan.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !plan.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("IsApprovedForExternalRunFalse", !plan.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("NoSchedulerOrPolling", !plan.SchedulerOrPolling, "Scheduler/polling must remain false."),
            Check("NoRuntimeShadowReplaySubmit", !plan.RuntimeShadowReplaySubmit, "Runtime shadow replay submit must remain false."),
            Check("NoOrderSubmission", !plan.OrderSubmission, "Order submission must remain false."),
            Check("NoTradingMutation", !plan.TradingMutation, "Trading mutation must remain false."),
            Check("ApiWorkerFakeGateway", plan.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentTrue", plan.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("AbortCriteriaPresent", plan.AbortCriteria.Count >= 8, "Abort criteria must be present."),
            Check("RollbackStepsPresent", plan.RollbackSteps.Count >= 5, "Rollback steps must be present."),
            Check("PostRunValidationStepsPresent", plan.PostRunValidationSteps.Count >= 5, "Post-run validation steps must be present.")
        };

        var combined = string.Join(" ", plan.PlanId, plan.RequestedByOperatorId, plan.Reason, plan.Symbol, plan.SlashSymbol, plan.PlanningSecurityId, plan.SecurityIdSource, plan.EnvironmentName, plan.VenueProfileName, plan.RequestMode, plan.SymbolEncodingMode, plan.AttemptGateDecision, plan.FutureCommandTemplate, plan.ApiWorkerGatewayMode, string.Join(" ", plan.AbortCriteria), string.Join(" ", plan.RollbackSteps), string.Join(" ", plan.PostRunValidationSteps));
        if (SensitivePattern.IsMatch(combined)) checks.Add(Fail("NoSensitiveContent", "Execution plan contains credential-shaped or sensitive content."));
        if (AuthorizationPattern.IsMatch(combined)) checks.Add(Fail("NoCurrentAuthorizationLanguage", "Execution plan must not imply current order, trading, external run, Production, UAT, or execution authorization."));

        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL)
            ? LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL
            : LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS;

        return new(final, plan with { Decision = final }, checks);
    }

    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlanCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdManualSnapshotExecutionPlanCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.FAIL, detail);
}
