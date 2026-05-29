using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate(
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
    string SourcePhase7DDecisionPath,
    string SourceEurgbpReadinessPath,
    string SourceExecutionChecklistPath,
    string SourceEurgbpReadinessDecision,
    string SourceExecutionChecklistDecision,
    string PreviousInstrument,
    string PreviousInstrumentClosureDecision,
    string PreviousDecision,
    bool OneInstrumentAtATime,
    bool BatchExecutionAllowed,
    bool ExternalRunAuthorized,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool IsApprovedForExternalRun,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool TradingMutation,
    bool GatewayRegistration,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision FinalDecision);

public sealed record LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateCheck(
    string Name,
    LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateValidation(
    LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision FinalDecision,
    LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate Gate,
    IReadOnlyList<LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateCheck> Checks);

public static class LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationOrRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\\s+(run|environment|authorization|execution)|uat\\s+(run|environment|authorization|execution)|environmentName\"?\\s*[:=]\\s*\"?(Production|UAT)|run\\s+is\\s+authorized|external\\s+run\\s+authorized|can\\s+run\\s+external|batch\\s+execution\\s+allowed|automatic\\s+retry|run\\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateValidation Validate(
        LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate gate,
        LmaxReadOnlyPostGbpusdNextInstrumentDecision? phase7DDecision = null,
        LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydration? readiness = null,
        LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist? checklist = null,
        string rawText = "")
    {
        var checks = new List<LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateCheck>
        {
            Check("OperatorAndReason", HasText(gate.RequestedByOperatorId) && HasText(gate.Reason), "Operator id and reason are required."),
            Check("EurgbpOnly", gate.Symbol == "EURGBP" && gate.SlashSymbol == "EUR/GBP", "Gate must be for EURGBP / EUR/GBP only."),
            Check("SecurityId4003", gate.PlanningSecurityId == "4003", "EURGBP SecurityID must be 4003."),
            Check("SecurityIdSource8", gate.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoProfile", gate.EnvironmentName == "Demo" && gate.VenueProfileName == "DemoLondon", "Environment must be Demo / DemoLondon."),
            Check("SnapshotProfile", gate.RequestMode == "SnapshotPlusUpdates" && gate.SymbolEncodingMode == "SecurityIdOnly" && gate.MarketDepth == 1, "Snapshot profile must be SnapshotPlusUpdates / SecurityIdOnly / MarketDepth=1."),
            Check("SourcePathsPresent", HasText(gate.SourcePhase7DDecisionPath) && HasText(gate.SourceEurgbpReadinessPath) && HasText(gate.SourceExecutionChecklistPath), "Source decision, readiness, and checklist paths are required."),
            Check("SourceDecisionsPass", gate.SourceEurgbpReadinessDecision == "PASS" && gate.SourceExecutionChecklistDecision == "PASS", "EURGBP readiness and execution checklist decisions must be PASS."),
            Check("PreviousDecisionChain", gate.PreviousInstrument == "GBPUSD" && gate.PreviousInstrumentClosureDecision == "PASS" && gate.PreviousDecision == "ProceedToEurgbpPlanning", "Previous chain must be GBPUSD PASS and ProceedToEurgbpPlanning."),
            Check("ManualSingleInstrumentOnly", gate.OneInstrumentAtATime && !gate.BatchExecutionAllowed, "One-instrument-at-a-time must be true and batch execution false."),
            Check("RunEligibilityFalse", !gate.ExternalRunAuthorized && !gate.CanRunExternalSnapshot && !gate.EligibleForManualSnapshotAttempt && !gate.IsApprovedForExternalRun, "Run authorization and eligibility flags must remain false."),
            Check("RuntimePowerFalse", !gate.SchedulerOrPolling && !gate.RuntimeShadowReplaySubmit && !gate.OrderSubmission && !gate.TradingMutation && !gate.GatewayRegistration, "Scheduler, runtime replay submit, order, mutation, and gateway registration flags must remain false."),
            Check("FakeGatewayOnly", gate.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentTrue", gate.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", gate.FinalDecision == LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.PASS, "Complete safe final pre-run gate should be PASS.")
        };

        if (phase7DDecision is null)
        {
            checks.Add(Fail("Phase7DDecisionProvided", "Phase 7D decision artifact is required for validation."));
        }
        else
        {
            checks.Add(Check("Phase7DProceedToEurgbp",
                phase7DDecision.Decision == LmaxReadOnlyPostGbpusdNextInstrumentDecisionOutcome.ProceedToEurgbpPlanning
                && phase7DDecision.CurrentInstrument == "GBPUSD"
                && phase7DDecision.NextCandidateInstrument == "EURGBP"
                && phase7DDecision.GbpusdClosureDecision == "PASS"
                && phase7DDecision.GbpusdClosureClassification == "CompletedWithBook"
                && !phase7DDecision.CanRunExternalSnapshot
                && !phase7DDecision.IsApprovedForExternalRun
                && !phase7DDecision.EligibleForManualSnapshotAttempt
                && !phase7DDecision.BatchExecutionAllowed
                && phase7DDecision.ExecutableCount == 0,
                "Phase 7D must safely proceed from GBPUSD CompletedWithBook PASS to EURGBP planning."));
        }

        if (readiness is null)
        {
            checks.Add(Fail("ReadinessProvided", "EURGBP readiness artifact is required for validation."));
        }
        else
        {
            checks.Add(Check("ReadinessMatchesGate",
                readiness.FinalDecision == LmaxReadOnlyAdditionalInstrumentManualSnapshotReadinessRehydrationDecision.PASS
                && readiness.SelectedInstrument == gate.Symbol
                && readiness.SlashSymbol == gate.SlashSymbol
                && readiness.SecurityId == gate.PlanningSecurityId
                && readiness.SecurityIdSource == gate.SecurityIdSource
                && readiness.EnvironmentName == gate.EnvironmentName
                && readiness.VenueProfileName == gate.VenueProfileName
                && readiness.RequestMode == gate.RequestMode
                && readiness.SymbolEncodingMode == gate.SymbolEncodingMode
                && readiness.MarketDepth == gate.MarketDepth
                && readiness.PreviousDecision == gate.PreviousDecision
                && readiness.PreviousInstrumentClosureDecision == gate.PreviousInstrumentClosureDecision
                && readiness.OneInstrumentAtATime
                && !readiness.BatchExecutionAllowed
                && !readiness.CanRunExternalSnapshot
                && !readiness.IsApprovedForExternalRun
                && !readiness.EligibleForManualSnapshotAttempt,
                "EURGBP readiness must match this gate and remain non-executable."));
        }

        if (checklist is null)
        {
            checks.Add(Fail("ChecklistProvided", "EURGBP execution checklist artifact is required for validation."));
        }
        else
        {
            checks.Add(Check("ChecklistMatchesGate",
                checklist.Decision == LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistDecision.PASS
                && checklist.Symbol == gate.Symbol
                && checklist.SlashSymbol == gate.SlashSymbol
                && checklist.PlanningSecurityId == gate.PlanningSecurityId
                && checklist.SecurityIdSource == gate.SecurityIdSource
                && checklist.RequestMode == gate.RequestMode
                && checklist.SymbolEncodingMode == gate.SymbolEncodingMode
                && checklist.MarketDepth == gate.MarketDepth
                && checklist.PreviousDecision == gate.PreviousDecision
                && checklist.PreviousInstrumentClosureDecision == gate.PreviousInstrumentClosureDecision
                && checklist.OneInstrumentAtATime
                && !checklist.BatchExecutionAllowed
                && !checklist.ExternalRunAuthorized
                && !checklist.CanRunExternalSnapshot
                && !checklist.EligibleForManualSnapshotAttempt
                && !checklist.IsApprovedForExternalRun
                && !checklist.SchedulerOrPolling
                && !checklist.RuntimeShadowReplaySubmit
                && !checklist.OrderSubmission
                && !checklist.TradingMutation,
                "EURGBP checklist must match this gate and remain non-executable."));
        }

        var combined = string.Join(" ",
            gate.GateId,
            gate.RequestedByOperatorId,
            gate.Reason,
            gate.Symbol,
            gate.SlashSymbol,
            gate.PlanningSecurityId,
            gate.SecurityIdSource,
            gate.EnvironmentName,
            gate.VenueProfileName,
            gate.RequestMode,
            gate.SymbolEncodingMode,
            gate.SourcePhase7DDecisionPath,
            gate.SourceEurgbpReadinessPath,
            gate.SourceExecutionChecklistPath,
            gate.SourceEurgbpReadinessDecision,
            gate.SourceExecutionChecklistDecision,
            gate.PreviousInstrument,
            gate.PreviousInstrumentClosureDecision,
            gate.PreviousDecision,
            gate.ApiWorkerGatewayMode,
            rawText);

        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveText", "Final pre-run gate contains credential-shaped or raw FIX content."));
        }

        if (AuthorizationOrRuntimePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoAuthorizationOrRuntimeText", "Final pre-run gate must not imply current authorization, automation, order, scheduler, production/UAT, runtime replay submit, or batch execution."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL)
            ? LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL
            : LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.PASS;

        return new(final, gate with { FinalDecision = final }, checks);
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateDecision.FAIL, detail);
}
