using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyGbpusdManualSnapshotFinalReadiness(
    string ReadinessId,
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
    string SourceAttemptGatePath,
    string SourceExecutionPlanPath,
    string SourceOperatorSignoffPath,
    string SourcePhase6TGatePath,
    string SourcePhase6UGatePath,
    string PlanningDecision,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    string DryRunDecision,
    string AttemptGateDecision,
    string ExecutionPlanDecision,
    string OperatorSignoffDecision,
    LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision ReadinessDecision,
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
    bool RuntimeShadowReplaySubmit,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    string RequiredFutureStep,
    string BlockingReason);

public sealed record LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck(
    string Name,
    LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyGbpusdManualSnapshotFinalReadinessResult(
    LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision FinalDecision,
    LmaxReadOnlyGbpusdManualSnapshotFinalReadiness Readiness,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck> Checks);

public sealed record LmaxReadOnlyGbpusdManualSnapshotFinalReadinessReviewResult(
    LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotFinalReadiness> ReadinessArtifacts,
    IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck> Issues);

public static class LmaxReadOnlyGbpusdManualSnapshotFinalReadinessValidator
{
    private static readonly Regex SensitivePattern = new("(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthorizationPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized|authorizes execution)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyGbpusdManualSnapshotFinalReadinessResult Validate(
        LmaxReadOnlyGbpusdManualSnapshotFinalReadiness readiness,
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest? planning,
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest? safety,
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest? preflight,
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? approval,
        LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport? dryRun,
        LmaxReadOnlySingleInstrumentSnapshotAttemptGate? attemptGate,
        LmaxReadOnlyGbpusdManualSnapshotExecutionPlan? executionPlan,
        LmaxReadOnlyGbpusdManualSnapshotOperatorSignoff? operatorSignoff,
        string? phase6TGateDecision,
        string? phase6UGateDecision)
    {
        var planningEntry = planning?.Instruments.FirstOrDefault(x => x.Symbol.Equals(readiness.Symbol, StringComparison.OrdinalIgnoreCase));
        var safetyEntry = safety?.Instruments.FirstOrDefault(x => x.Symbol.Equals(readiness.Symbol, StringComparison.OrdinalIgnoreCase));
        var preflightEntry = preflight?.Results.FirstOrDefault(x => x.Symbol.Equals(readiness.Symbol, StringComparison.OrdinalIgnoreCase));
        var checks = new List<LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck>
        {
            Check("PlanningManifestExists", planning is not null, "Planning manifest is required."),
            Check("SafetyGateManifestExists", safety is not null, "Safety gate manifest is required."),
            Check("PreflightManifestExists", preflight is not null, "Preflight manifest is required."),
            Check("ApprovalEnvelopeExists", approval is not null, "Approval envelope is required."),
            Check("DryRunReportExists", dryRun is not null, "Dry-run report is required."),
            Check("AttemptGateExists", attemptGate is not null, "Attempt gate is required."),
            Check("ExecutionPlanExists", executionPlan is not null, "Execution plan is required."),
            Check("OperatorSignoffExists", operatorSignoff is not null, "Operator signoff is required."),
            Check("GbpusdOnly", readiness.Symbol == "GBPUSD" && readiness.SlashSymbol == "GBP/USD", "Final readiness must be for GBPUSD / GBP/USD only."),
            Check("SecurityId4002", readiness.PlanningSecurityId == "4002", "GBPUSD SecurityID must be 4002."),
            Check("SecurityIdSource8", readiness.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", readiness.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", readiness.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", readiness.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", readiness.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", readiness.MarketDepth == 1, "MarketDepth must be 1."),
            Check("SourceSymbolAgreement", Same(readiness.Symbol, planningEntry?.Symbol, safetyEntry?.Symbol, preflightEntry?.Symbol, approval?.Symbol, dryRun?.Symbol, attemptGate?.Symbol, executionPlan?.Symbol, operatorSignoff?.Symbol), "All source artifacts must agree on symbol."),
            Check("SourceSecurityIdAgreement", Same(readiness.PlanningSecurityId, planningEntry?.PlanningSecurityId, safetyEntry?.PlanningSecurityId, preflightEntry?.PlanningSecurityId, approval?.PlanningSecurityId, dryRun?.PlanningSecurityId, attemptGate?.PlanningSecurityId, executionPlan?.PlanningSecurityId, operatorSignoff?.PlanningSecurityId), "All source artifacts must agree on SecurityID."),
            Check("PlanningAccepted", planningEntry?.Decision == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning && readiness.PlanningDecision == "AcceptedForPlanning", "Planning decision must be AcceptedForPlanning."),
            Check("SafetyGatePass", safetyEntry?.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS && readiness.SafetyGateDecision == "PASS", "Safety gate must be PASS."),
            Check("PreflightPass", preflightEntry?.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS && readiness.PreflightDecision == "PASS", "Preflight must be PASS."),
            Check("ApprovalAccepted", approval?.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning && readiness.ApprovalEnvelopeDecision == "AcceptedForPlanning", "Approval envelope must be AcceptedForPlanning."),
            Check("DryRunPass", dryRun?.DryRunDecision == LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS && readiness.DryRunDecision == "PASS", "Dry-run must be PASS."),
            Check("AttemptGatePass", attemptGate?.GateDecision == LmaxReadOnlySingleInstrumentSnapshotAttemptGateDecision.PASS && readiness.AttemptGateDecision == "PASS", "Attempt gate must be PASS."),
            Check("ExecutionPlanPass", executionPlan?.Decision == LmaxReadOnlyGbpusdManualSnapshotExecutionPlanDecision.PASS && readiness.ExecutionPlanDecision == "PASS", "Execution plan must be PASS."),
            Check("OperatorSignoffSignedForPlanning", operatorSignoff?.SignoffDecision == LmaxReadOnlyGbpusdManualSnapshotOperatorSignoffDecision.SignedForPlanning && readiness.OperatorSignoffDecision == "SignedForPlanning", "Operator signoff must be SignedForPlanning."),
            Check("Phase6TGatePass", string.IsNullOrWhiteSpace(readiness.SourcePhase6TGatePath) || phase6TGateDecision == "PASS", "Phase 6T gate must be PASS when supplied."),
            Check("Phase6UGatePass", string.IsNullOrWhiteSpace(readiness.SourcePhase6UGatePath) || phase6UGateDecision == "PASS", "Phase 6U gate must be PASS when supplied."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(readiness.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(readiness.Reason), "Reason is required."),
            Check("IsApprovedForExternalRunFalse", !readiness.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !readiness.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !readiness.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoExternalConnectionAttempted", !readiness.ExternalConnectionAttempted, "External connection must not be attempted."),
            Check("NoSnapshotAttempted", !readiness.SnapshotAttempted, "Snapshot must not be attempted."),
            Check("NoReplayAttempted", !readiness.ReplayAttempted, "Replay must not be attempted."),
            Check("NoOrderSubmissionAttempted", !readiness.OrderSubmissionAttempted, "Order submission must not be attempted."),
            Check("NoShadowReplaySubmitAttempted", !readiness.ShadowReplaySubmitAttempted && !readiness.RuntimeShadowReplaySubmit, "Shadow replay submit must not be attempted."),
            Check("NoTradingMutationAttempted", !readiness.TradingMutationAttempted, "Trading mutation must not be attempted."),
            Check("NoSchedulerStarted", !readiness.SchedulerStarted, "Scheduler must not start."),
            Check("ApiWorkerFakeGateway", readiness.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentTrue", readiness.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        var combined = string.Join(" ", readiness.ReadinessId, readiness.RequestedByOperatorId, readiness.Reason, readiness.Symbol, readiness.SlashSymbol, readiness.PlanningSecurityId, readiness.SecurityIdSource, readiness.EnvironmentName, readiness.VenueProfileName, readiness.RequestMode, readiness.SymbolEncodingMode, readiness.PlanningDecision, readiness.SafetyGateDecision, readiness.PreflightDecision, readiness.ApprovalEnvelopeDecision, readiness.DryRunDecision, readiness.AttemptGateDecision, readiness.ExecutionPlanDecision, readiness.OperatorSignoffDecision, readiness.RequiredFutureStep, readiness.BlockingReason);
        if (SensitivePattern.IsMatch(combined)) checks.Add(Fail("NoSensitiveContent", "Final readiness contains credential-shaped or sensitive content."));
        if (AuthorizationPattern.IsMatch(combined)) checks.Add(Fail("NoCurrentAuthorizationLanguage", "Final readiness must not imply current order, trading, external run, Production, UAT, or execution authorization."));

        var final = checks.Any(x => x.Decision == LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL)
            ? LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL
            : LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS;
        return new(final, readiness with { ReadinessDecision = final }, checks);
    }

    public static LmaxReadOnlyGbpusdManualSnapshotFinalReadinessReviewResult Review(IReadOnlyList<LmaxReadOnlyGbpusdManualSnapshotFinalReadiness> readinessArtifacts)
    {
        if (readinessArtifacts.Count == 0) return new(LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS_WITH_KNOWN_WARNINGS, readinessArtifacts, []);
        var issues = readinessArtifacts
            .Where(x => x.ReadinessDecision == LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL || x.IsApprovedForExternalRun || x.EligibleForManualSnapshotAttempt || x.CanRunExternalSnapshot || x.ExternalConnectionAttempted || x.SnapshotAttempted || x.ReplayAttempted || x.OrderSubmissionAttempted || x.ShadowReplaySubmitAttempted || x.RuntimeShadowReplaySubmit || x.TradingMutationAttempted || x.SchedulerStarted)
            .Select(x => Fail("UnsafeFinalReadiness", $"{x.Symbol} final readiness is unsafe or executable."))
            .ToArray();
        return new(issues.Length == 0 ? LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS : LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, readinessArtifacts, issues);
    }

    private static bool Same(string expected, params string?[] values)
        => values.All(value => !string.IsNullOrWhiteSpace(value) && string.Equals(expected, value, StringComparison.OrdinalIgnoreCase));

    private static LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyGbpusdManualSnapshotFinalReadinessCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyGbpusdManualSnapshotFinalReadinessDecision.FAIL, detail);
}
