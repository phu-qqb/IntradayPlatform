using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport(
    string DryRunReportId,
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
    int MaxRuntimeSeconds,
    int MaxWaitSeconds,
    int MaxEventsPerRun,
    string SourcePlanningManifestPath,
    string SourceSafetyGateManifestPath,
    string SourcePreflightManifestPath,
    string SourceApprovalEnvelopePath,
    string PlanningDecision,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision DryRunDecision,
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
    bool NoSensitiveContent,
    string RequiredFutureStep,
    string BlockingReason);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotDryRunResult(
    LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision FinalDecision,
    LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport Report,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck> Checks);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReviewResult(
    LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport> Reports,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck> Issues);

public static class LmaxReadOnlyAdditionalInstrumentSnapshotDryRunValidator
{
    private static readonly Regex SensitivePattern = new("(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunResult Validate(
        LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport report,
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest planning,
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest safety,
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest preflight,
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? envelope)
    {
        var planningEntry = planning.Instruments.FirstOrDefault(x => x.Symbol.Equals(report.Symbol, StringComparison.OrdinalIgnoreCase));
        var safetyEntry = safety.Instruments.FirstOrDefault(x => x.Symbol.Equals(report.Symbol, StringComparison.OrdinalIgnoreCase));
        var preflightEntry = preflight.Results.FirstOrDefault(x => x.Symbol.Equals(report.Symbol, StringComparison.OrdinalIgnoreCase));
        var checks = new List<LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck>
        {
            Check("ApprovalEnvelopeExists", envelope is not null, "Approval envelope is required."),
            Check("ApprovalEnvelopeAcceptedForPlanning", envelope?.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning && report.ApprovalEnvelopeDecision == "AcceptedForPlanning", "Approval envelope must be AcceptedForPlanning."),
            Check("PlanningValueMatches", planningEntry is not null && report.Symbol == "GBPUSD" && report.PlanningSecurityId == planningEntry.PlanningSecurityId && report.PlanningSecurityId == "4002", "GBPUSD planning SecurityID must be 4002."),
            Check("SlashSymbolMatches", planningEntry is not null && report.SlashSymbol == planningEntry.SlashSymbol, "Slash symbol must match planning manifest."),
            Check("SecurityIdSource8", report.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", report.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", report.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", report.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", report.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", report.MarketDepth == 1, "MarketDepth must be 1."),
            Check("MaxRuntimeSecondsSafeCap", report.MaxRuntimeSeconds is >= 1 and <= 30, "MaxRuntimeSeconds must be 1..30."),
            Check("MaxWaitSecondsSafeCap", report.MaxWaitSeconds is >= 1 and <= 30, "MaxWaitSeconds must be 1..30."),
            Check("MaxEventsPerRunSafeCap", report.MaxEventsPerRun is >= 1 and <= 25, "MaxEventsPerRun must be 1..25."),
            Check("PlanningDecisionAccepted", report.PlanningDecision == "AcceptedForPlanning", "Planning decision must be AcceptedForPlanning."),
            Check("SafetyGatePass", safetyEntry?.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS && report.SafetyGateDecision == "PASS", "Safety gate must be PASS."),
            Check("PreflightPass", preflightEntry?.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS && report.PreflightDecision == "PASS", "Preflight must be PASS."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(report.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(report.Reason), "Reason is required."),
            Check("IsApprovedForExternalRunFalse", !report.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !report.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !report.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoExternalConnectionAttempted", !report.ExternalConnectionAttempted, "External connection must not be attempted."),
            Check("NoSnapshotAttempted", !report.SnapshotAttempted, "Snapshot must not be attempted."),
            Check("NoReplayAttempted", !report.ReplayAttempted, "Replay must not be attempted."),
            Check("NoOrderSubmissionAttempted", !report.OrderSubmissionAttempted, "Order submission must not be attempted."),
            Check("NoShadowReplaySubmitAttempted", !report.ShadowReplaySubmitAttempted, "Shadow replay submit must not be attempted."),
            Check("NoTradingMutationAttempted", !report.TradingMutationAttempted, "Trading mutation must not be attempted."),
            Check("NoSchedulerStarted", !report.SchedulerStarted, "Scheduler must not start."),
            Check("NoSensitiveContentTrue", report.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        var combined = string.Join(" ", report.DryRunReportId, report.RequestedByOperatorId, report.Reason, report.Symbol, report.SlashSymbol, report.PlanningSecurityId, report.SecurityIdSource, report.EnvironmentName, report.VenueProfileName, report.RequestMode, report.SymbolEncodingMode, report.PlanningDecision, report.SafetyGateDecision, report.PreflightDecision, report.ApprovalEnvelopeDecision, report.RequiredFutureStep, report.BlockingReason);
        if (SensitivePattern.IsMatch(combined)) checks.Add(Fail("NoSensitiveContent", "Dry-run report contains credential-shaped or sensitive content."));
        if (AuthorizationPattern.IsMatch(combined)) checks.Add(Fail("NoTradingOrExternalAuthorizationLanguage", "Dry-run report must not imply order, trading, external run, Production, UAT, or execution authorization."));

        var final = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS;
        return new(final, report with { DryRunDecision = final }, checks);
    }

    public static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReviewResult Review(IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport> reports)
    {
        if (reports.Count == 0) return new(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS_WITH_KNOWN_WARNINGS, reports, []);
        var issues = reports
            .Where(x => x.DryRunDecision == LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL || x.CanRunExternalSnapshot || x.IsApprovedForExternalRun || x.EligibleForManualSnapshotAttempt || x.ExternalConnectionAttempted || x.SnapshotAttempted || x.ReplayAttempted || x.OrderSubmissionAttempted || x.ShadowReplaySubmitAttempted || x.TradingMutationAttempted || x.SchedulerStarted)
            .Select(x => Fail("UnsafeDryRunReport", $"{x.Symbol} dry-run report is unsafe or executable."))
            .ToArray();
        return new(issues.Length == 0 ? LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS : LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, reports, issues);
    }

    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, detail);
}
