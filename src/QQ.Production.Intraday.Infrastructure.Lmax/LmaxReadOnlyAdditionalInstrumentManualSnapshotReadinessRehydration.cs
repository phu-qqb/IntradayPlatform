using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration(
    string RehydrationId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string SourcePhase7DDecisionFile,
    string SourcePipelineManifestFile,
    string SourcePlanningManifestFile,
    string SourceSafetyGateManifestFile,
    string SourcePreflightManifestFile,
    string SelectedInstrument,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    string PreviousInstrument,
    string PreviousInstrumentClosureDecision,
    string PreviousDecision,
    string NextCandidateInstrument,
    string PipelineDecision,
    string PlanningDecision,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    string DryRunDecision,
    string AttemptGateDecision,
    string ExecutionPlanDecision,
    string OperatorSignoffDecision,
    string FinalReadinessDecision,
    string ApprovalEnvelopePath,
    string DryRunReportPath,
    string AttemptGatePath,
    string ExecutionPlanPath,
    string OperatorSignoffPath,
    string FinalReadinessPath,
    bool OneInstrumentAtATime,
    bool BatchExecutionAllowed,
    int ExecutableCount,
    bool IsApprovedForExternalRun,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool ExternalConnectionAttempted,
    bool SnapshotAttempted,
    bool ReplayAttempted,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    bool NoSensitiveContent,
    LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidation(
    LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationCheck> Checks);

public static class LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationOrRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\\s+(run|environment|authorization|execution)|uat\\s+(run|environment|authorization|execution)|environmentName\"?\\s*[:=]\\s*\"?(Production|UAT)|run\\s+is\\s+authorized|external\\s+run\\s+authorized|can\\s+run\\s+external|batch\\s+execution\\s+allowed|automatic\\s+retry|run\\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationValidation Validate(
        LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration rehydration,
        string rawText = "")
    {
        var checks = new List<LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationCheck>
        {
            Check("OperatorAndReason", HasText(rehydration.RequestedByOperatorId) && HasText(rehydration.Reason), "Operator id and reason are required."),
            Check("SourceFilesPresent", HasText(rehydration.SourcePhase7DDecisionFile) && HasText(rehydration.SourcePipelineManifestFile) && HasText(rehydration.SourcePlanningManifestFile) && HasText(rehydration.SourceSafetyGateManifestFile) && HasText(rehydration.SourcePreflightManifestFile), "Source artifact references are required."),
            Check("SelectedInstrumentEurgbp", rehydration.SelectedInstrument == "EURGBP" && rehydration.SlashSymbol == "EUR/GBP", "Selected instrument must be EURGBP / EUR/GBP."),
            Check("SecurityId4003", rehydration.SecurityId == "4003", "EURGBP SecurityID must be 4003."),
            Check("SecurityIdSource8", rehydration.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoProfile", rehydration.EnvironmentName == "Demo" && rehydration.VenueProfileName == "DemoLondon", "Environment must be Demo / DemoLondon."),
            Check("SnapshotProfile", rehydration.RequestMode == "SnapshotPlusUpdates" && rehydration.SymbolEncodingMode == "SecurityIdOnly" && rehydration.MarketDepth == 1, "Snapshot profile must be SnapshotPlusUpdates / SecurityIdOnly / MarketDepth=1."),
            Check("Phase7DProceed", rehydration.PreviousInstrument == "GBPUSD" && rehydration.PreviousInstrumentClosureDecision == "PASS" && rehydration.PreviousDecision == "ProceedToEurgbpPlanning" && rehydration.NextCandidateInstrument == "EURGBP", "Phase 7D must proceed from safe GBPUSD closure to EURGBP."),
            Check("PipelineAndSourceDecisions", rehydration.PipelineDecision == "PASS" && rehydration.PlanningDecision == "AcceptedForPlanning" && rehydration.SafetyGateDecision == "PASS" && rehydration.PreflightDecision == "PASS", "Pipeline, planning, safety, and preflight decisions must be safe expected values."),
            Check("ArtifactBundleDecisions", rehydration.ApprovalEnvelopeDecision == "AcceptedForPlanning" && rehydration.DryRunDecision == "PASS" && rehydration.AttemptGateDecision == "PASS" && rehydration.ExecutionPlanDecision == "PASS" && rehydration.OperatorSignoffDecision == "SignedForPlanning" && rehydration.FinalReadinessDecision == "PASS", "EURGBP planning artifact bundle decisions must be safe expected values."),
            Check("ArtifactBundlePaths", HasText(rehydration.ApprovalEnvelopePath) && HasText(rehydration.DryRunReportPath) && HasText(rehydration.AttemptGatePath) && HasText(rehydration.ExecutionPlanPath) && HasText(rehydration.OperatorSignoffPath) && HasText(rehydration.FinalReadinessPath), "EURGBP planning artifact bundle paths are required."),
            Check("ManualSingleInstrumentOnly", rehydration.OneInstrumentAtATime && !rehydration.BatchExecutionAllowed, "One-instrument-at-a-time must be true and batch execution false."),
            Check("ExecutableCountZero", rehydration.ExecutableCount == 0, "Executable count must remain zero."),
            Check("RunEligibilityFalse", !rehydration.IsApprovedForExternalRun && !rehydration.CanRunExternalSnapshot && !rehydration.EligibleForManualSnapshotAttempt, "Run eligibility flags must remain false."),
            Check("AttemptFlagsFalse", !rehydration.ExternalConnectionAttempted && !rehydration.SnapshotAttempted && !rehydration.ReplayAttempted && !rehydration.OrderSubmissionAttempted && !rehydration.ShadowReplaySubmitAttempted && !rehydration.TradingMutationAttempted && !rehydration.SchedulerStarted, "Attempt, order, replay, scheduler, and mutation flags must remain false."),
            Check("NoSensitiveContent", rehydration.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", rehydration.FinalDecision == LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS, "Final decision must be PASS for complete safe rehydration.")
        };

        if (SensitivePattern.IsMatch(rawText))
        {
            checks.Add(Fail("NoSensitiveText", "Rehydration artifact contains credential-shaped or raw FIX content."));
        }

        if (AuthorizationOrRuntimePattern.IsMatch(rawText))
        {
            checks.Add(Fail("NoAuthorizationOrRuntimeText", "Rehydration artifact contains authorization, runtime, scheduler, order, production/UAT, or batch language."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS;
        return new(final, checks);
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.FAIL, detail);
}
