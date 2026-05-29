using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision
{
    Draft,
    SignedForPlanning,
    Rejected,
    Invalid
}

public enum LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff(
    string SignoffId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string SignedByOperatorId,
    string SignoffRole,
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
    string SourceExecutionPlanPath,
    string SourceExecutionPlanDecision,
    string SourcePhase6TGateReportPath,
    string SourcePhase6TGateDecision,
    bool ConfirmsExecutionPlanReviewed,
    bool ConfirmsKillRollbackPlanReviewed,
    bool ConfirmsDemoOnly,
    bool ConfirmsReadOnlyMarketDataOnly,
    bool ConfirmsSingleInstrumentOnly,
    bool ConfirmsNoOrderSubmission,
    bool ConfirmsNoSchedulerOrPolling,
    bool ConfirmsNoRuntimeShadowReplaySubmit,
    bool ConfirmsNoTradingMutation,
    bool ConfirmsNoGatewayRegistration,
    bool ConfirmsCredentialValuesMustRemainRedacted,
    bool ConfirmsFutureManualExecutionPhaseRequired,
    LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision SignoffDecision,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool CanRunExternalSnapshot,
    bool ExternalConnectionAttempted,
    bool SnapshotAttempted,
    bool ReplayAttempted,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck(
    string Name,
    LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffResult(
    LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision FinalDecision,
    LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff Signoff,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck> Checks);

public sealed record LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffReviewResult(
    LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff> Signoffs,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck> Issues);

public static class LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidator
{
    private static readonly Regex SensitivePattern = new("(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized|authorizes execution)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff signoff,
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan? executionPlan,
        string? phase6TGateDecision)
    {
        var signed = signoff.SignoffDecision == LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.SignedForPlanning;
        var attestations = new[]
        {
            signoff.ConfirmsExecutionPlanReviewed,
            signoff.ConfirmsKillRollbackPlanReviewed,
            signoff.ConfirmsDemoOnly,
            signoff.ConfirmsReadOnlyMarketDataOnly,
            signoff.ConfirmsSingleInstrumentOnly,
            signoff.ConfirmsNoOrderSubmission,
            signoff.ConfirmsNoSchedulerOrPolling,
            signoff.ConfirmsNoRuntimeShadowReplaySubmit,
            signoff.ConfirmsNoTradingMutation,
            signoff.ConfirmsNoGatewayRegistration,
            signoff.ConfirmsCredentialValuesMustRemainRedacted,
            signoff.ConfirmsFutureManualExecutionPhaseRequired
        };

        var checks = new List<LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck>
        {
            Check("ExecutionPlanExists", executionPlan is not null, "Execution plan is required."),
            Check("ExecutionPlanPass", executionPlan?.Decision == LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS && signoff.SourceExecutionPlanDecision == "PASS", "Execution plan must be PASS."),
            Check("Phase6TGatePass", string.IsNullOrWhiteSpace(signoff.SourcePhase6TGateReportPath) || (phase6TGateDecision == "PASS" && signoff.SourcePhase6TGateDecision == "PASS"), "Phase 6T gate must be PASS when supplied."),
            Check("GbpusdOnly", signoff.Symbol == "GBPUSD" && signoff.SlashSymbol == "GBP/USD", "Signoff must be for GBPUSD / GBP/USD only."),
            Check("SecurityId4002", signoff.PlanningSecurityId == "4002", "GBPUSD SecurityID must be 4002."),
            Check("SecurityIdSource8", signoff.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", signoff.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", signoff.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", signoff.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", signoff.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", signoff.MarketDepth == 1, "MarketDepth must be 1."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(signoff.Reason), "Reason is required."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(signoff.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("SignedByRequiredForSignedForPlanning", !signed || !string.IsNullOrWhiteSpace(signoff.SignedByOperatorId), "SignedByOperatorId is required for SignedForPlanning."),
            Check("AllAttestationsForSignedForPlanning", !signed || attestations.All(x => x), "All planning attestations are required for SignedForPlanning."),
            Check("IsApprovedForExternalRunFalse", !signoff.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !signoff.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !signoff.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoExternalConnectionAttempted", !signoff.ExternalConnectionAttempted, "External connection must not be attempted."),
            Check("NoSnapshotAttempted", !signoff.SnapshotAttempted, "Snapshot must not be attempted."),
            Check("NoReplayAttempted", !signoff.ReplayAttempted, "Replay must not be attempted."),
            Check("NoOrderSubmissionAttempted", !signoff.OrderSubmissionAttempted, "Order submission must not be attempted."),
            Check("NoShadowReplaySubmitAttempted", !signoff.ShadowReplaySubmitAttempted, "Shadow replay submit must not be attempted."),
            Check("NoTradingMutationAttempted", !signoff.TradingMutationAttempted, "Trading mutation must not be attempted."),
            Check("NoSchedulerStarted", !signoff.SchedulerStarted, "Scheduler must not start."),
            Check("NoSensitiveContentTrue", signoff.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        var combined = string.Join(" ", signoff.SignoffId, signoff.RequestedByOperatorId, signoff.SignedByOperatorId, signoff.SignoffRole, signoff.Reason, signoff.Symbol, signoff.SlashSymbol, signoff.PlanningSecurityId, signoff.SecurityIdSource, signoff.EnvironmentName, signoff.VenueProfileName, signoff.RequestMode, signoff.SymbolEncodingMode, signoff.SourceExecutionPlanDecision, signoff.SourcePhase6TGateDecision, signoff.SignoffDecision);
        if (SensitivePattern.IsMatch(combined)) checks.Add(Fail("NoSensitiveContent", "Operator signoff contains credential-shaped or sensitive content."));
        if (AuthorizationPattern.IsMatch(combined)) checks.Add(Fail("NoCurrentAuthorizationLanguage", "Operator signoff must not imply current order, trading, external run, Production, UAT, or execution authorization."));

        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL)
            ? LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL
            : LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS;
        return new(final, signoff, checks);
    }

    public static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffReviewResult Review(IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff> signoffs)
    {
        if (signoffs.Count == 0) return new(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS_WITH_KNOWN_WARNINGS, signoffs, []);
        var issues = signoffs
            .Where(x => x.SignoffDecision == LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.Invalid || x.IsApprovedForExternalRun || x.EligibleForManualSnapshotAttempt || x.CanRunExternalSnapshot || x.ExternalConnectionAttempted || x.SnapshotAttempted || x.ReplayAttempted || x.OrderSubmissionAttempted || x.ShadowReplaySubmitAttempted || x.TradingMutationAttempted || x.SchedulerStarted)
            .Select(x => Fail("UnsafeOperatorSignoff", $"{x.Symbol} operator signoff is unsafe or executable."))
            .ToArray();
        var hasSigned = signoffs.Any(x => x.SignoffDecision == LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.SignedForPlanning);
        if (issues.Length > 0) return new(LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, signoffs, issues);
        return new(hasSigned ? LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS : LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS_WITH_KNOWN_WARNINGS, signoffs, issues);
    }

    private static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffValidationDecision.FAIL, detail);
}
