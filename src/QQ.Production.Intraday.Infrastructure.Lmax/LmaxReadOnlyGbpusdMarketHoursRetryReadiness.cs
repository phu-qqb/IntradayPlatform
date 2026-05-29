using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdMarketHoursRetryReadiness(
    string RetryReadinessId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string SourceFinalReadinessFile,
    string SourcePhase6XReviewFile,
    string PreviousResultStatus,
    bool PreviousAttemptWasOutsideMarketHours,
    bool RetryAllowedOnlyDuringMarketHours,
    bool RetryIsManualOnly,
    int RetryAttemptCount,
    bool NoScheduler,
    bool NoPolling,
    bool NoRuntimeShadowReplaySubmit,
    bool NoOrderSubmission,
    bool NoTradingMutation,
    string ApiWorkerGatewayMode,
    bool CanRunAutomatically,
    bool NoSensitiveContent,
    string FutureCommandTemplate,
    string RequiredFutureStep,
    string BlockingReason,
    LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision Decision);

public sealed record LmaxReadOnlyGbpusdMarketHoursRetryReadinessCheck(
    string Name,
    LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidation(
    LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision FinalDecision,
    LmaxReadOnlyGbpusdMarketHoursRetryReadiness Readiness,
    IReadOnlyList<LmaxReadOnlyGbpusdMarketHoursRetryReadinessCheck> Checks);

public static class LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidator
{
    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UnsafeAuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatusrequest|submitorder|order submission|scheduler enabled|polling enabled|automatic retry|run automatically|runtime shadow replay submit|trading mutation|production|uat)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdMarketHoursRetryReadinessValidation Validate(
        LmaxReadOnlyGbpusdMarketHoursRetryReadiness readiness,
        string? phase6XReviewDecision,
        string rawReadinessText = "")
    {
        var checks = new List<LmaxReadOnlyGbpusdMarketHoursRetryReadinessCheck>
        {
            Check("GbpusdOnly", readiness.Symbol == "GBPUSD" && readiness.SlashSymbol == "GBP/USD", "Retry readiness must be for GBPUSD / GBP/USD."),
            Check("SecurityId4002", readiness.SecurityId == "4002", "GBPUSD SecurityID must be 4002."),
            Check("SecurityIdSource8", readiness.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("SourceFinalReadinessPresent", !string.IsNullOrWhiteSpace(readiness.SourceFinalReadinessFile), "Source final readiness file is required."),
            Check("SourcePhase6XReviewPresent", !string.IsNullOrWhiteSpace(readiness.SourcePhase6XReviewFile), "Source Phase 6X review file is required."),
            Check("PreviousEmptyBook", readiness.PreviousResultStatus == "CompletedWithEmptyBook", "Previous result must be CompletedWithEmptyBook."),
            Check("Phase6XWarningDecision", string.IsNullOrWhiteSpace(phase6XReviewDecision) || phase6XReviewDecision == "PASS_WITH_KNOWN_WARNINGS", "Phase 6X review must be PASS_WITH_KNOWN_WARNINGS."),
            Check("PreviousAttemptOutsideMarketHours", readiness.PreviousAttemptWasOutsideMarketHours, "Previous attempt must be marked outside market hours."),
            Check("MarketHoursOnly", readiness.RetryAllowedOnlyDuringMarketHours, "Retry must be restricted to market hours."),
            Check("ManualOnly", readiness.RetryIsManualOnly, "Retry must be manual-only."),
            Check("SingleRetryAttempt", readiness.RetryAttemptCount == 1, "Retry attempt count must be exactly one."),
            Check("NoScheduler", readiness.NoScheduler, "Scheduler must remain disabled."),
            Check("NoPolling", readiness.NoPolling, "Polling must remain disabled."),
            Check("NoRuntimeShadowReplaySubmit", readiness.NoRuntimeShadowReplaySubmit, "Runtime shadow replay submit must remain disabled."),
            Check("NoOrderSubmission", readiness.NoOrderSubmission, "Order submission must remain disabled."),
            Check("NoTradingMutation", readiness.NoTradingMutation, "Trading mutation must remain disabled."),
            Check("FakeGatewayOnly", readiness.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("CannotRunAutomatically", !readiness.CanRunAutomatically, "Readiness must not be automatic or scheduled."),
            Check("NoSensitiveContent", readiness.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FutureCommandMarkedNonExecutable", readiness.FutureCommandTemplate.Contains("DO NOT RUN FROM THIS SCRIPT", StringComparison.OrdinalIgnoreCase), "Future command must be clearly marked non-executable by the preparation script."),
            Check("RequiredFutureStep", readiness.RequiredFutureStep.Contains("Phase 6Z", StringComparison.OrdinalIgnoreCase), "Required future step must point to Phase 6Z operator-approved execution."),
            Check("BlockingReason", readiness.BlockingReason.Contains("does not run GBPUSD", StringComparison.OrdinalIgnoreCase), "Blocking reason must state this phase does not run GBPUSD."),
            Check("DecisionPass", readiness.Decision == LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.PASS, "Retry readiness decision must be PASS when complete.")
        };

        if (SensitivePattern.IsMatch(rawReadinessText))
        {
            checks.Add(Fail("NoSensitiveText", "Readiness text contains credential-shaped content."));
        }

        var authorizationScanText = rawReadinessText
            .Replace("NoOrderSubmission", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("NoRuntimeShadowReplaySubmit", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("NoTradingMutation", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("no scheduler/polling", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (UnsafeAuthorizationPattern.IsMatch(authorizationScanText))
        {
            checks.Add(Fail("NoUnsafeAuthorizationLanguage", "Readiness text contains unsafe authorization or trading language."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL)
            ? LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL
            : LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.PASS;
        return new(final, readiness, checks);
    }

    private static LmaxReadOnlyGbpusdMarketHoursRetryReadinessCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdMarketHoursRetryReadinessCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdMarketHoursRetryReadinessDecision.FAIL, detail);
}
