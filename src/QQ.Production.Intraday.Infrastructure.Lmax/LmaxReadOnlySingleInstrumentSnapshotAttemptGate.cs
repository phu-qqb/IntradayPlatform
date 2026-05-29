using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlySingleInstrumentSnapshotAttemptGate(
    string GateId,
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
    string SourcePlanningManifestPath,
    string SourceSafetyGateManifestPath,
    string SourcePreflightManifestPath,
    string SourceApprovalEnvelopePath,
    string SourceDryRunReportPath,
    string PlanningDecision,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    string DryRunDecision,
    LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision GateDecision,
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

public sealed record LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck(
    string Name,
    LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision Decision,
    string Detail);

public sealed record LmaxReadOnlySingleInstrumentSnapshotAttemptGateResult(
    LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision FinalDecision,
    LmaxReadOnlySingleInstrumentSnapshotAttemptGate Gate,
    IReadOnlyList<LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck> Checks);

public sealed record LmaxReadOnlySingleInstrumentSnapshotAttemptGateReviewResult(
    LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlySingleInstrumentSnapshotAttemptGate> Gates,
    IReadOnlyList<LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck> Issues);

public static class LmaxReadOnlySingleInstrumentSnapshotAttemptGateValidator
{
    private static readonly Regex SensitivePattern = new("(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlySingleInstrumentSnapshotAttemptGateResult Validate(
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate gate,
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest? planning,
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest? safety,
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest? preflight,
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? approval,
        LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport? dryRun)
    {
        var planningEntry = planning?.Instruments.FirstOrDefault(x => x.Symbol.Equals(gate.Symbol, StringComparison.OrdinalIgnoreCase));
        var safetyEntry = safety?.Instruments.FirstOrDefault(x => x.Symbol.Equals(gate.Symbol, StringComparison.OrdinalIgnoreCase));
        var preflightEntry = preflight?.Results.FirstOrDefault(x => x.Symbol.Equals(gate.Symbol, StringComparison.OrdinalIgnoreCase));

        var checks = new List<LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck>
        {
            Check("PlanningManifestExists", planning is not null, "Planning manifest is required."),
            Check("SafetyGateManifestExists", safety is not null, "Safety gate manifest is required."),
            Check("PreflightManifestExists", preflight is not null, "Preflight manifest is required."),
            Check("ApprovalEnvelopeExists", approval is not null, "Approval envelope is required."),
            Check("DryRunReportExists", dryRun is not null, "Dry-run report is required."),
            Check("PlanningValueMatches", planningEntry is not null && gate.Symbol == "GBPUSD" && gate.PlanningSecurityId == planningEntry.PlanningSecurityId && gate.PlanningSecurityId == "4002", "GBPUSD planning SecurityID must be 4002."),
            Check("SafetyGatePass", safetyEntry?.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS && gate.SafetyGateDecision == "PASS", "Safety gate must be PASS."),
            Check("PreflightPass", preflightEntry?.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS && gate.PreflightDecision == "PASS", "Preflight must be PASS."),
            Check("ApprovalEnvelopeAcceptedForPlanning", approval?.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning && gate.ApprovalEnvelopeDecision == "AcceptedForPlanning", "Approval envelope must be AcceptedForPlanning."),
            Check("DryRunPass", dryRun?.DryRunDecision == LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS && gate.DryRunDecision == "PASS", "Dry-run report must be PASS."),
            Check("SourceSymbolAgreement", Same(gate.Symbol, planningEntry?.Symbol, safetyEntry?.Symbol, preflightEntry?.Symbol, approval?.Symbol, dryRun?.Symbol), "All source artifacts must refer to the same symbol."),
            Check("SourceSlashSymbolAgreement", Same(gate.SlashSymbol, planningEntry?.SlashSymbol, safetyEntry?.SlashSymbol, preflightEntry?.SlashSymbol, approval?.SlashSymbol, dryRun?.SlashSymbol), "All source artifacts must refer to the same slash symbol."),
            Check("SourceSecurityIdAgreement", Same(gate.PlanningSecurityId, planningEntry?.PlanningSecurityId, safetyEntry?.PlanningSecurityId, preflightEntry?.PlanningSecurityId, approval?.PlanningSecurityId, dryRun?.PlanningSecurityId), "All source artifacts must refer to the same SecurityID."),
            Check("SecurityIdSource8", gate.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", gate.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", gate.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", gate.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", gate.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", gate.MarketDepth == 1, "MarketDepth must be 1."),
            Check("PlanningDecisionAccepted", gate.PlanningDecision == "AcceptedForPlanning", "Planning decision must be AcceptedForPlanning."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(gate.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(gate.Reason), "Reason is required."),
            Check("IsApprovedForExternalRunFalse", !gate.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !gate.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !gate.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoExternalConnectionAttempted", !gate.ExternalConnectionAttempted, "External connection must not be attempted."),
            Check("NoSnapshotAttempted", !gate.SnapshotAttempted, "Snapshot must not be attempted."),
            Check("NoReplayAttempted", !gate.ReplayAttempted, "Replay must not be attempted."),
            Check("NoOrderSubmissionAttempted", !gate.OrderSubmissionAttempted, "Order submission must not be attempted."),
            Check("NoShadowReplaySubmitAttempted", !gate.ShadowReplaySubmitAttempted, "Shadow replay submit must not be attempted."),
            Check("NoTradingMutationAttempted", !gate.TradingMutationAttempted, "Trading mutation must not be attempted."),
            Check("NoSchedulerStarted", !gate.SchedulerStarted, "Scheduler must not start."),
            Check("NoSensitiveContentTrue", gate.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        var combined = string.Join(" ", gate.GateId, gate.RequestedByOperatorId, gate.Reason, gate.Symbol, gate.SlashSymbol, gate.PlanningSecurityId, gate.SecurityIdSource, gate.EnvironmentName, gate.VenueProfileName, gate.RequestMode, gate.SymbolEncodingMode, gate.PlanningDecision, gate.SafetyGateDecision, gate.PreflightDecision, gate.ApprovalEnvelopeDecision, gate.DryRunDecision, gate.RequiredFutureStep, gate.BlockingReason);
        if (SensitivePattern.IsMatch(combined)) checks.Add(Fail("NoSensitiveContent", "Attempt gate contains credential-shaped or sensitive content."));
        if (AuthorizationPattern.IsMatch(combined)) checks.Add(Fail("NoTradingOrExternalAuthorizationLanguage", "Attempt gate must not imply order, trading, external run, Production, UAT, or execution authorization."));

        var final = checks.Any(x => x.Decision == LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL)
            ? LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL
            : LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS;

        return new(final, gate with { GateDecision = final }, checks);
    }

    public static LmaxReadOnlySingleInstrumentSnapshotAttemptGateReviewResult Review(IReadOnlyList<LmaxReadOnlySingleInstrumentSnapshotAttemptGate> gates)
    {
        if (gates.Count == 0) return new(LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS_WITH_KNOWN_WARNINGS, gates, []);

        var issues = gates
            .Where(x => x.GateDecision == LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL || x.CanRunExternalSnapshot || x.IsApprovedForExternalRun || x.EligibleForManualSnapshotAttempt || x.ExternalConnectionAttempted || x.SnapshotAttempted || x.ReplayAttempted || x.OrderSubmissionAttempted || x.ShadowReplaySubmitAttempted || x.TradingMutationAttempted || x.SchedulerStarted)
            .Select(x => Fail("UnsafeAttemptGate", $"{x.Symbol} attempt gate is unsafe or executable."))
            .ToArray();

        return new(issues.Length == 0 ? LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS : LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, gates, issues);
    }

    private static bool Same(string expected, params string?[] values)
        => values.All(value => !string.IsNullOrWhiteSpace(value) && string.Equals(expected, value, StringComparison.OrdinalIgnoreCase));

    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlySingleInstrumentSnapshotAttemptGateCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.FAIL, detail);
}
