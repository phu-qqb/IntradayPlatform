using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPack(
    string ChecklistId,
    DateTimeOffset CreatedAtUtc,
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string RequiredManualCommand,
    string ManualCommandWarning,
    IReadOnlyList<string> PreRunChecks,
    IReadOnlyList<string> DuringRunMonitoring,
    IReadOnlyList<string> PostRunSequence,
    IReadOnlyList<string> AbortCriteria,
    IReadOnlyList<string> RollbackSteps,
    IReadOnlyList<string> ExplicitNonAuthorizations,
    bool CanRunAutomatically,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision FinalDecision);

public sealed record LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackCheck(
    string Name,
    LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidation(
    LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackCheck> Checks);

public static class LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForbiddenCommandPattern = new(
        "(scheduler|polling|ReplaySubmitAsync|SubmitToShadowReplay|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest|TradeCaptureReportRequest|SubmitOrder|production|uat|batch)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackValidation Validate(
        LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPack pack,
        string rawPackText = "")
    {
        var checks = new List<LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackCheck>
        {
            Check("GbpusdIdentity", pack.Symbol == "GBPUSD" && pack.SlashSymbol == "GBP/USD" && pack.SecurityId == "4002" && pack.SecurityIdSource == "8", "Checklist must be for GBPUSD / GBP/USD / 4002 / SecurityIDSource=8."),
            Check("ManualWrapperCommand", Contains(pack.RequiredManualCommand, "run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1") && Contains(pack.RequiredManualCommand, "FinalReadinessFile") && Contains(pack.RequiredManualCommand, "AllowExternalConnections") && Contains(pack.RequiredManualCommand, "ConfirmDemoReadOnly"), "Command must be the existing explicit GBPUSD wrapper command."),
            Check("MarketHoursWarning", Contains(pack.ManualCommandWarning, "DO NOT RUN UNTIL MARKET HOURS"), "Checklist must clearly mark the command as not runnable until market hours."),
            Check("PreRunChecks", HasItem(pack.PreRunChecks, "market hours") && HasItem(pack.PreRunChecks, "FakeLmaxGateway") && HasItem(pack.PreRunChecks, "final readiness PASS") && HasItem(pack.PreRunChecks, "Phase 7C closure scripts"), "Pre-run checks must cover market hours, FakeLmaxGateway, final readiness, and Phase 7C scripts."),
            Check("KillSwitch", HasItem(pack.DuringRunMonitoring, "Ctrl+C") || HasItem(pack.DuringRunMonitoring, "close process"), "During-run monitoring must include a kill switch."),
            Check("OneAttemptOnly", HasItem(pack.DuringRunMonitoring, "one attempt") && HasItem(pack.DuringRunMonitoring, "no retry"), "During-run monitoring must enforce one attempt and no retry."),
            Check("PostRunPhase7CSequence", HasItem(pack.PostRunSequence, "review") && HasItem(pack.PostRunSequence, "evidence preview") && HasItem(pack.PostRunSequence, "closure manifest") && HasItem(pack.PostRunSequence, "Phase 7C gate") && HasItem(pack.PostRunSequence, "Phase 7D"), "Post-run sequence must include review, evidence preview, closure manifest, Phase 7C gate, and Phase 7D decision."),
            Check("Rollback", HasItem(pack.RollbackSteps, "stop process") && HasItem(pack.RollbackSteps, "FakeLmaxGateway") && HasItem(pack.RollbackSteps, "no DB rollback"), "Rollback must include stopping process, verifying FakeLmaxGateway, and no DB rollback expected."),
            Check("NonAuthorizations", HasItem(pack.ExplicitNonAuthorizations, "scheduler") && HasItem(pack.ExplicitNonAuthorizations, "polling") && HasItem(pack.ExplicitNonAuthorizations, "orders") && HasItem(pack.ExplicitNonAuthorizations, "gateway registration") && HasItem(pack.ExplicitNonAuthorizations, "multi-instrument batch"), "Explicit non-authorizations must cover scheduler, polling, orders, gateway registration, and batch."),
            Check("NoRuntimePower", !pack.CanRunAutomatically && !pack.SchedulerOrPolling && !pack.RuntimeShadowReplaySubmit && !pack.OrderSubmission && !pack.GatewayRegistration && !pack.TradingMutation, "Checklist pack must not enable automatic run, scheduler, replay submit, orders, gateway, or mutation."),
            Check("FakeGatewayOnly", pack.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentFlag", pack.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", pack.FinalDecision == LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.PASS, "Complete checklist pack should be PASS.")
        };

        if (ForbiddenCommandPattern.IsMatch(pack.RequiredManualCommand))
        {
            checks.Add(Fail("NoForbiddenCommandSurface", "Manual command contains scheduler, order, runtime replay submit, production/UAT, or batch wording."));
        }

        if (SensitivePattern.IsMatch(rawPackText))
        {
            checks.Add(Fail("NoSensitiveText", "Checklist pack contains credential-shaped or raw FIX content."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL)
            ? LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL
            : LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.PASS;
        return new(final, checks);
    }

    private static bool Contains(string value, string expected)
        => value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasItem(IEnumerable<string> values, string expected)
        => values.Any(x => Contains(x, expected));

    private static LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdMarketHoursExecutionChecklistPackDecision.FAIL, detail);
}
